# Design

## Context

Motivación y alcance en `proposal.md`. Estado de partida relevante, tras `add-agent-guardrails`:

- `SalesAgent.RunAsync` hace **una sola** llamada a `FunctionInvokingChatClient.GetResponseAsync`. Envuelve el modelo en `RunBudgetChatClient` (límites de llamadas y tokens) y usa un `FunctionInvoker` propio que aplica la allow-list, cuenta los errores seguidos (con `Terminate`) y emite `ToolInvoked`. Al terminar, ordena las tools por los `FunctionCallContent` de la respuesta y detecta las desconocidas.
- `RunState` (privado de `SalesAgent`) guarda la allow-list efectiva, las invocaciones por `CallId`, los errores seguidos y el motivo de parada.
- `AddSandboxApplication` registra `ISalesCommands` **decorado con el inyector de fallos**; `SalesCommands` también está registrado como tipo concreto.
- El Runner es *one-shot*: `AgentRunner` resuelve la petición del escenario o de `--prompt` y llama al agente una vez. La salida estándar es para el resultado y los registros van a la de errores.
- En `sales-agent`, "Escenarios con nombre" ocupa 496 caracteres, cerca del límite de 500 de la validación estricta.

### Explore: experimentos y resultados

Spike en el scratchpad (`spike-fase5b`), con `Microsoft.Extensions.AI` 10.11.0, un modelo falso y sin red. Documentación oficial consultada el 2026-11-05: `FunctionInvokingChatClient` (observaciones sobre aprobación) y `ApprovalRequiredAIFunction`.

| Exp. | Qué se probó | Resultado observado |
|---|---|---|
| A1 | El modelo pide `cancel_order`, envuelta en `ApprovalRequiredAIFunction` | `GetResponseAsync` vuelve tras 1 llamada con un `ToolApprovalRequestContent` (`RequestId` = `ficc_<CallId>`) que envuelve el `FunctionCallContent` exacto. El `FunctionInvoker` no se llama |
| A2 | Reanudar con `request.CreateResponse(true)` en un mensaje de usuario | La invocación **pasa por el `FunctionInvoker`** (`ctx.Function` es el `ApprovalRequiredAIFunction`), con los argumentos de la petición. El modelo recibe un historial normal (petición y resultado), sin los contenidos de aprobación |
| A3 | Reanudar con `CreateResponse(false, motivo)` | **No pasa por el `FunctionInvoker`.** El modelo recibe como resultado: `Tool call invocation was rejected by the approver and must not be retried. <motivo>` |
| A4 | `ping` y `cancel_order` en la misma respuesta | **Todo o nada**: las dos llegan como `ToolApprovalRequestContent`. Al aprobar las dos, se ejecutan las dos |
| A5 | Compilar con los avisos tratados como errores y sin silenciar `MEAI001` | Sin avisos: estos tipos no son experimentales en la 10.11.0 |

La documentación confirma A1, A2 y A4 (el "todo o nada" está descrito, con `AllowMultipleToolCalls = false` como mitigación) y que `ApprovalRequiredAIFunction` "no hace cumplir" la aprobación por sí sola. El texto exacto de A3 es observado, no documentado.

## Goals / Non-Goals

**Goals:**
- Que ninguna ejecución de `cancel_order` ocurra sin una decisión humana explícita sobre esa petición concreta.
- Usar el mecanismo estándar de la librería y conservar todos los guardrails del change anterior sobre lo aprobado.
- Que todo se pruebe sin LLM y sin teclado.

**Non-Goals:**
- Persistir la conversación para aprobar más tarde o desde otro proceso.
- Aprobaciones por lotes, reglas de "aprobar siempre" o autoaprobación heurística (las trae Agent Framework, que no usamos).
- Tiempo de espera de la aprobación.

## Decisions

### D1. Pausa y reanudación con `ApprovalRequiredAIFunction`

Las tools que requieren confirmación se ofrecen envueltas en `ApprovalRequiredAIFunction`. El bucle devuelve el control con las peticiones de aprobación (A1), el agente decide y lo reanuda con las respuestas (A2, A3).

Alternativa descartada (en el explore): esperar al aprobador dentro del `FunctionInvoker`. Es menos código, la denegación sería un `ToolError` propio y no existiría el todo o nada. Pero solo sirve con un aprobador que contesta en el momento, no enseña el mecanismo estándar y no prepara el patrón de aprobación fuera de banda (web, chat), en el que la conversación sale del proceso y vuelve.

### D2. Bucle de reanudación en `SalesAgent`

`RunAsync` pasa de una llamada a un bucle:

1. Llama a `GetResponseAsync` con el historial y acumula los mensajes de la respuesta.
2. Si un guardrail paró la ejecución (excepción de presupuesto o `Terminate` por errores seguidos), termina.
3. Si la respuesta no tiene `ToolApprovalRequestContent`, termina con `Completed`.
4. Si los tiene, añade los mensajes de la respuesta al historial y decide cada petición (D3). Después añade un mensaje de usuario con todas las `ToolApprovalResponseContent` y vuelve al paso 1.

- El historial acumulado (todas las respuestas) sustituye a la respuesta única en lo que ya existía: textos intermedios, orden de las tools y detección de tools desconocidas.
- `RunBudgetChatClient` es la misma instancia en todas las vueltas, así que los límites de llamadas y tokens cubren el total. `MaximumIterationsPerRequest` se reinicia en cada `GetResponseAsync`, pero el cliente de presupuesto sigue cortando a `MaxModelCalls` (D6 del change anterior).
- `RunState` es el mismo en todas las vueltas, así que los errores seguidos se cuentan a través de las pausas.

### D3. Qué se decide en cada petición

- **Tool que requiere confirmación** (`SalesToolNames.RequiresConfirmation`, que solo contiene `cancel_order`): se pregunta a `IToolApprover` con el nombre y los argumentos del `FunctionCallContent` de la petición.
- **Cualquier otra tool** (todo o nada, A4): se aprueba en código, sin preguntar ni auditar como decisión. Llegará al `FunctionInvoker`, que aplica la allow-list y audita la invocación como siempre.
- **Denegación**: la respuesta lleva como motivo "El cliente no ha confirmado esta acción; no la repitas y explícale que no se ha hecho.". `RunState` registra la tool con `CONFIRMATION_DENIED` en su `CallId`, para que aparezca en su sitio en la lista, y la cuenta como error seguido. Si con ella se alcanza `MaxConsecutiveToolErrors`, la ejecución termina con `ToolErrorLimit` **sin reanudar**, es decir, sin volver a llamar al modelo.
- **Auditoría**: cada decisión del aprobador emite `ApprovalDecided` (D6). Las aprobaciones en código no lo emiten: no hubo decisión humana.

Los argumentos que se aprueban son los que se ejecutan, porque la librería invoca el `FunctionCallContent` que va dentro de la petición y el modelo nunca ve los contenidos de aprobación (A2). El agente no modifica esos argumentos entre la petición y la respuesta. Un test de aprendizaje fija que se ejecutan los argumentos aprobados.

### D4. Solo se envuelve lo que está permitido

Una tool que requiere confirmación se envuelve en `ApprovalRequiredAIFunction` solo si la allow-list la permite. Si no, queda como `AIFunction` normal en `AdditionalTools` y el `FunctionInvoker` la bloquea con `BLOCKED_BY_POLICY` (D8 del change anterior), sin preguntar a nadie.

Alternativa descartada: envolverla siempre y denegarla en el paso de aprobación si no está permitida. Mezclaría la allow-list con la confirmación y cambiaría el código que recibe el modelo en el escenario `read-only`.

### D5. El aprobador: contrato en `Sandbox.Agents`, consola en el Runner

- `IToolApprover.DecideAsync(ToolApprovalRequest, CancellationToken) -> ToolApprovalDecision`:
  - la petición lleva el nombre de la tool y sus argumentos;
  - la decisión lleva si se aprueba, quién decide (`Console`, `NonInteractive` o el nombre del guion) y el motivo, si lo hay.
  - El agente depende del contrato y no sabe nada de consolas.
- `ConsoleToolApprover` (en `Sandbox.Runner`) recibe la entrada, la salida de errores y una función que dice si la entrada es interactiva; en producción, `!Console.IsInputRedirected`. Así se prueba con un `StringReader`, sin consola.
  - Sin consola interactiva, deniega con el motivo "sin consola interactiva" y **no lee** la entrada.
  - Con consola, escribe por la salida de errores `El agente quiere ejecutar <tool> con <argumentos en JSON>. ¿Lo apruebas? [s/N]` y lee una línea. Solo `s`, `si` o `sí` (sin distinguir mayúsculas, recortada) aprueban (`sí` se añadió en el verify: es lo que escribe un hispanohablante); el resto, también una línea vacía o el fin de la entrada, deniega.
  - Los argumentos se serializan con `ToolJson.SerializerOptions`. El codificador relajado deja legibles los acentos y escapa igualmente los caracteres de control. Un test lo fija con `ESC` (U+001B), que en una terminal puede iniciar una secuencia de control.
- Los tests usan `ScriptedToolApprover`, que decide según un guion y guarda las peticiones recibidas.

### D6. Evento de auditoría `ApprovalDecided`

`AuditLog` gana el evento 5005 `ApprovalDecided`:
- campos: tool, decisión (`Approved` o `Denied`, un enum nuevo `ApprovalOutcome` en `Sandbox.Application.Auditing`), aprobador y motivo;
- sin argumentos, igual que `ToolInvoked`: la acción exacta la vio el humano, pero la auditoría no guarda los datos del pedido.

Lleva el *scope* de ejecución como el resto.

### D7. Pedidos previos de un escenario

- `AgentScenario` gana `SeedOrders`: pedidos con cliente y líneas.
- `AgentRunner` los crea antes de llamar al agente, con `SalesCommands` (el tipo concreto, sin el inyector de fallos) y claves de idempotencia `seed-<n>` en el ámbito de cada cliente.
- Si un pedido previo falla, es un error del catálogo de escenarios, no del modelo: se lanza una excepción, que el Runner traduce al código 1.

Alternativa descartada: crearlos con `ISalesCommands`. Las reglas `FirstCalls` del inyector (como la de `lost-response`) se consumirían con los pedidos previos.

### D8. Descripción de `cancel_order`, sin tocar el system prompt

La descripción de `cancel_order` le dice al modelo que la llame directamente, sin pedir confirmación por texto, porque el sistema ya la pide, y que no repita la petición si no se confirma. La primera redacción ("Antes de cancelar, el sistema pide confirmación al cliente…") **no bastó**: ver Hallazgos del apply.

El system prompt no cambia: el requisito "Instrucciones del sistema sin reglas de negocio" (432 caracteres) superaría el límite con otra viñeta, y la librería ya avisa de que no se reintente (A3).

### D9. Códigos de salida

- Una denegación no es la parada de un guardrail: la ejecución termina con `Completed` y el código 0.
- Si varias denegaciones seguidas alcanzan `MaxConsecutiveToolErrors`, la ejecución termina con `ToolErrorLimit` y el código 4, como cualquier otro error seguido.

## Risks / Trade-offs

- [La consola bloquea la ejecución hasta que alguien contesta] -> Es lo esperado en un Runner interactivo; Ctrl+C cancela. Sin consola interactiva, deniega al instante.
- [`docker compose run` podría no abrir una consola interactiva] -> Por lo que sé, `compose run` es interactivo por defecto (**no verificado**). Se comprueba en el apply. Si no lo fuera, el aprobador denegaría, que es el comportamiento seguro, y se documentaría la opción necesaria.
- [El texto de denegación de la librería está en inglés y el motivo en español] -> El modelo recibe los dos. Se observará contra DeepSeek si respeta el "no lo repitas".
- [`FunctionCallContent.Arguments` es un diccionario mutable] -> Si el código del agente lo modificara entre la petición y la respuesta, se ejecutaría algo distinto de lo aprobado. El agente no lo toca, y el test de aprendizaje fija que se ejecutan los argumentos aprobados.
- [El historial crece con cada pausa] -> Una pausa por acción irreversible; los límites de llamadas y tokens siguen cubriendo el total.
- [Varias aprobaciones en una misma respuesta (dos `cancel_order`)] -> Se pregunta por cada una, en orden. Cada una tiene su decisión y su evento.

## Migration Plan

Sin datos persistentes ni despliegue. Los escenarios existentes no piden `cancel_order`, así que su comportamiento no cambia. Para deshacerlo basta con revertir el PR.

## Open Questions

Ninguna pendiente. Respondidas en el apply (ver Hallazgos del apply): `docker compose run` abre una consola interactiva por defecto, y DeepSeek respeta la denegación sin reintentar.

## Hallazgos del apply

Ejecuciones reales contra DeepSeek (`deepseek-flash`), 2026-11-05:

- **El modelo pedía la confirmación por texto en lugar de llamar a la tool.** Con la primera descripción de `cancel_order`, DeepSeek consultó el pedido (`get_order`) y respondió "¿Confirmas que quieres cancelarlo?" sin pedir `cancel_order`: en una ejecución de un solo turno, la petición quedaba sin hacer. Pasó en dos ejecuciones (una en compose desde las herramientas del asistente y otra del usuario en compose con la imagen sin reconstruir). La descripción se reformuló para pedir que la llame directamente porque el sistema ya pide la confirmación. Desde entonces, en las 5 ejecuciones el modelo llamó a `cancel_order` en la primera respuesta. Es un ejemplo de que el texto de una tool cambia el comportamiento del modelo más que el diseño del bucle.
- **Sin consola interactiva, el aprobador deniega y el modelo no reintenta**: dos ejecuciones desde las herramientas del asistente (entrada no interactiva) terminaron con `cancel_order (CONFIRMATION_DENIED)`, 2 llamadas y unos 2.700 tokens de entrada; el modelo explicó que el pedido seguía en curso.
- **Con una persona en la consola** (ejecuciones del usuario): al aprobar con `s`, el pedido se cancela (2 llamadas, 2.841 tokens de entrada); al pulsar Intro, se deniega con el motivo "el cliente no lo aprobó" y el modelo no reintenta (2 llamadas, 2.696 tokens).
- **`docker compose run` abre una consola interactiva por defecto**: el contenedor preguntó `[s/N]` y la aprobación funcionó igual que con `dotnet run` (2 llamadas, 2.795 tokens). La ayuda de `compose run` (Compose v5.6.1) no muestra el valor por defecto; se comprobó de forma empírica. Ojo: sin `--build`, compose reutiliza la imagen ya construida aunque el código haya cambiado.
- **Auditoría legible al aprobar**: el aprobador no da motivo al aprobar, y la consola mostraba `Motivo: (null)`. Ahora se audita "sin motivo".
