# Design

## Context

Motivación y alcance en `proposal.md`. Estado de partida relevante, tras la fase 4:

- `SalesAgent.RunAsync` monta en cada ejecución este pipeline: `FunctionInvokingChatClient` -> `CallCountingChatClient` -> el `IChatClient` de DeepSeek. Usa los límites por defecto de la librería y un `FunctionInvoker` propio, el borde exterior de las tools, que registra cada invocación y convierte los argumentos inválidos en `INVALID_ARGUMENTS`.
- `SalesTools` construye `GetOrderQuery(OrderId)` y `CancelOrderCommand(IdempotencyKey, OrderId)` sin el cliente de sesión, aunque `Order.CustomerId` ya existe en el dominio (ADR 0004, "limitación conocida").
- `IdempotencyStore` indexa por clave, de forma global. `SalesCommands.Execute` comprueba la repetición **antes** de ejecutar la operación, dentro de la sección crítica.
- El Runner traduce las excepciones a códigos de salida (0, 1, 2 y 3) y suma los fallos de un escenario a los configurados con `PostConfigure`.
- `RecordingLoggerProvider`, el registro de los tests, ignora los *scopes*.

### Explore: experimentos y resultados

Spike en el scratchpad (`spike-fase5`), con `Microsoft.Extensions.AI` 10.11.0, un modelo falso y sin red. Documentación oficial consultada el 2026-11-02 (`MaximumIterationsPerRequest`, `MaximumConsecutiveErrorsPerRequest`, `AdditionalTools`, `TerminateOnUnknownCalls`).

| Exp. | Qué se probó | Resultado observado |
|---|---|---|
| E1 | `MaximumIterationsPerRequest = 3`, modelo que siempre pide una tool | 4 llamadas al modelo: al llegar al límite, la librería hace una llamada extra **sin tools**. Si el modelo aun así pide una tool, la respuesta termina en una petición sin ejecutar y sin texto |
| E2 | Tool que devuelve un error tipado (sin lanzar), `MaximumConsecutiveErrorsPerRequest = 1` | No cuenta como error: 6 llamadas, hasta el límite de iteraciones |
| E3 | Tool que lanza, mismo límite | Corta y relanza la excepción de la tool al llamador |
| E4 | `context.Terminate = true` en el `FunctionInvoker` | Termina tras esa invocación, sin más llamadas al modelo; la respuesta acaba en el resultado de la tool |
| E5 | Tool que no está en `ChatOptions.Tools` | La librería responde `Error: Requested function "ship_order" not found.` y **no pasa por el `FunctionInvoker`** |
| E6 | Tool en `AdditionalTools`, no ofrecida | No se envía al modelo (solo las de `ChatOptions.Tools`), pero si el modelo la pide **sí pasa por el `FunctionInvoker`**, con `CallContent.CallId` e `Iteration` disponibles |
| E7 | Un cliente intermedio lanza una excepción propia antes de la tercera llamada | La excepción llega al llamador sin envolver, tras 2 llamadas |

La documentación confirma E2 y E3 (`MaximumConsecutiveErrorsPerRequest` cuenta excepciones) y E5/E6 (`AdditionalTools` se consulta para peticiones de tools no ofrecidas). No describe la llamada extra de E1: eso es observado, no documentado.

## Goals / Non-Goals

**Goals:**
- Que cada guardrail sea determinista, configurable donde tiene sentido y probado sin LLM.
- Que la autorización por recurso la decida la aplicación, no el agente ni el prompt.
- Que una ejecución parada por un guardrail devuelva un resultado completo (motivo, llamadas, tools y tokens), no una excepción.
- Que todo lo que el modelo pida, incluso lo que no se ejecuta, deje rastro en la auditoría.

**Non-Goals:**
- Un almacén de auditoría duradero o a prueba de manipulación: la auditoría es un registro estructurado.
- Límites por tiempo o por coste en dinero: solo tokens.
- Sustituir el `FunctionInvokingChatClient` por un bucle propio.

## Decisions

### D1. La autorización por recurso se decide en la aplicación

`GetOrderQuery` pasa a `(OrderId, CustomerId)` y `CancelOrderCommand` a `(IdempotencyKey, OrderId, CustomerId)`. `SalesQueries` y `SalesCommands` buscan el pedido y, si `Order.CustomerId` no coincide, devuelven el mismo `SalesErrors.OrderNotFound()` que para un pedido inexistente. En `CancelOrder` la comprobación ocurre dentro de la sección crítica, junto con la transición: no hay carrera entre comprobar y cancelar. `ShipOrder` no cambia: es una operación del almacén, sin cliente.

Alternativas descartadas:
- **En la tool** (leer el pedido y comparar): mete lógica en una tool que debe ser fina (AGENTS.md, sección 4.6) y abre una carrera entre la lectura y la cancelación.
- **En un guardrail del `FunctionInvoker`**: el guardrail tendría que consultar el dominio, lo que acopla la capa de invocación del LLM a las reglas de negocio. Rompe la separación de AGENTS.md (principio 1) y deja sin proteger a cualquier otro llamador de la aplicación.

Esta decisión, la de D2 y la de D3 trascienden el change (los escenarios de ataque de la fase 6 dependen de ellas): se registran en el **ADR 0005**.

### D2. Un pedido ajeno es indistinguible de uno inexistente

El modelo recibe `ORDER_NOT_FOUND` con el mismo mensaje. Así no se confirma a un tercero que un pedido existe, que es la defensa habitual frente a la enumeración de recursos, más relevante aún con números secuenciales. El motivo real no se pierde: la aplicación registra un evento de auditoría de acceso denegado (D4).

Alternativa descartada: un código propio `FORBIDDEN`, como preveía el documento 06. Es más explícito al depurar, pero el modelo puede contarle al usuario que ese pedido existe y es de otro cliente.

### D3. Las claves de idempotencia se guardan por ámbito

`IdempotencyStore` se indexa por `(ámbito, clave)`. El ámbito es un tipo propio con dos variantes: cliente (con su identificador) en `PlaceOrder` y `CancelOrder`, y almacén en `ShipOrder`. Un tipo, y no un prefijo de texto, evita colisiones entre un identificador de cliente y el nombre del almacén. La repetición sigue comprobándose antes de ejecutar, pero ya solo encuentra las claves del mismo ámbito. Por eso la comprobación de pertenencia de D1 se ejecuta siempre que la petición venga de otro cliente.

Alternativa descartada: meter el cliente en la huella de argumentos con la clave global. Otro cliente recibiría `IDEMPOTENCY_KEY_CONFLICT`, lo que le confirma que esa clave existe.

Efecto colateral aceptado: antes, la misma clave en `PlaceOrder` y luego en `ShipOrder` daba conflicto; ahora son ámbitos distintos y no lo da. La spec solo exigía el conflicto entre commands del mismo cliente ("Misma clave en otro command" usa `CancelOrder`), que se mantiene.

### D4. Auditoría con `ILogger`, categoría propia y *scopes*

- Categoría `Sandbox.Audit`, definida en `Sandbox.Application` (que ya depende de `Microsoft.Extensions.Logging.Abstractions`) para que la compartan la aplicación y el agente sin que el dominio conozca nada de IA.
- Eventos con `[LoggerMessage]` e identificadores estables:
  - `ToolInvoked`: tool, decisión, resultado y duración;
  - `UnknownToolRequested`: nombre recortado a 64 caracteres;
  - `RunStopped`: motivo;
  - `AccessDenied`: operación, pedido, cliente y motivo.
- Correlación: `SalesAgent.RunAsync` abre un *scope* con `RunId` (un `Guid` nuevo por ejecución) y `CustomerId`. Los *scopes* viajan con el flujo asíncrono, así que también los llevan los eventos que emite la aplicación sin saber nada de la ejecución. El Runner activa `IncludeScopes` en la consola.
- El evento `ToolInvoked` sustituye a la línea de registro actual de cada invocación, que cumplía el requisito "Registro de cada invocación" y ahora pasa a la auditoría.
- `RecordingLoggerProvider` pasa a capturar los *scopes* activos (pila en `AsyncLocal`) en cada `LogEntry`.

Alternativa descartada (en el explore): una abstracción `IAuditTrail`. Separaría mejor la auditoría del registro operativo, como en un sistema real, a costa de una pieza más. OpenTelemetry (tercer change) exportará igualmente estos registros.

### D5. Opciones de los guardrails en `Sandbox.Agents`

- `GuardrailOptions` (sección `Guardrails`) con `MaxModelCalls`, `MaxConsecutiveToolErrors`, `TokenBudget` y `AllowedTools`. Su validador está en `Sandbox.Agents`, y el Runner lo registra con `ValidateOnStart` (código 2).
- `AllowedTools` es `IList<string>?` y `null` significa "todas". Si se inicializara con las seis, el *binder* de configuración **añadiría** los valores configurados a los de por defecto en lugar de sustituirlos.
- La allow-list de un escenario se aplica en el Runner con `PostConfigure`, igual que los fallos inducidos: la lista efectiva es la intersección de la configurada (o todas) con la del escenario. La validación corre después de `PostConfigure`, así que una intersección vacía también da el código 2 (*fail closed*).
- `SalesToolNames` marca de forma explícita qué tools son de lectura (AGENTS.md, sección 4.7). El escenario `read-only` usa esa lista.

### D6. Un cliente de presupuesto para las llamadas y los tokens

`CallCountingChatClient` pasa a ser `RunBudgetChatClient`, también un `IChatClient` propio por la misma razón de la fase 4: no debe liberar el cliente compartido. Antes de cada llamada comprueba `calls >= MaxModelCalls` y `tokens >= TokenBudget`. Si salta un límite, lanza una excepción interna (`GuardrailStopException`, con el motivo) sin llamar al cliente interior. Después de cada llamada acumula `Usage`. Además, `FunctionInvokingChatClient.MaximumIterationsPerRequest` se fija a `MaxModelCalls`: la llamada extra de la librería (E1) es justo la que bloquea nuestro cliente, así que nunca se superan `MaxModelCalls` llamadas.

- **Por qué una excepción**: el límite se decide en el cliente de chat, antes de la llamada, y desde ahí no hay otra forma de cortar el bucle. E7 muestra que llega sin envolver. `SalesAgent` la captura y construye el resultado con los contadores del propio cliente y la lista de invocaciones, que no dependen de la respuesta.
- **Tokens del resultado**: salen siempre del cliente de presupuesto, no de `response.Usage`, para que coincidan también en las ejecuciones paradas.
- Alternativa descartada: usar solo `MaximumIterationsPerRequest`. Hace N+1 llamadas (E1), no deja saber que se alcanzó el límite y no cubre los tokens.
- Alternativa descartada: contar las iteraciones en el `FunctionInvoker` (`context.Iteration`) y usar `Terminate`. Sirve para las llamadas, pero no para los tokens, y dejaría los dos límites en sitios distintos.

### D7. Errores de tool seguidos, contados en el `FunctionInvoker`

El `FunctionInvoker` ya calcula el resultado de cada invocación. Un contador por ejecución suma uno con cualquier código de error (incluido `BLOCKED_BY_POLICY`) y vuelve a cero con un éxito. Al llegar a `MaxConsecutiveToolErrors`, pone `context.Terminate = true` (E4) y anota el motivo `ToolErrorLimit`. `MaximumConsecutiveErrorsPerRequest` se deja en su valor por defecto: solo actúa con excepciones (E2, E3), y el borde de las tools ya las convierte en `INTERNAL_ERROR`.

### D8. Allow-list: ofrecer las permitidas y bloquear las demás en el `FunctionInvoker`

- `ChatOptions.Tools` lleva las tools permitidas y `FunctionInvokingChatClient.AdditionalTools` el resto del catálogo (E6).
- Si el modelo pide una tool no permitida, el `FunctionInvoker` la ve y devuelve, sin invocarla, un `ToolResult` con `ToolError("BLOCKED_BY_POLICY", "Esta acción no está disponible en esta conversación.", Retryable: false)`. Después la audita con la decisión `Blocked`.
- Sin `AdditionalTools`, la petición tomaría el camino de E5: fuera del contrato `ToolError` y sin auditoría.

### D9. Tools desconocidas, detectadas al terminar

Un nombre que no está en el catálogo sigue el camino de E5 (texto de la librería, el bucle continúa). El `FunctionInvoker` anota el `CallId` de cada invocación que ve. Al terminar, `SalesAgent` recorre los `FunctionCallContent` de la respuesta en orden: los que tienen un `CallId` anotado toman su resultado, y los demás pasan a `UNKNOWN_TOOL`, con su evento de auditoría. Así la lista de tools del resultado queda en el orden en que las pidió el modelo.

Alternativa descartada: `TerminateOnUnknownCalls = true`. Pararía la ejecución ante un nombre inventado, que es un error recuperable del modelo; preferimos que el bucle siga y quede rastro.

### D10. Motivo de parada y texto fijo

- `AgentStopReason` tiene los valores `Completed`, `ModelCallLimit`, `ToolErrorLimit` y `TokenBudget`, y `AgentRunResult` lo incorpora.
- Si para un guardrail, la respuesta final es un texto fijo por motivo (por ejemplo, "No he podido terminar de atender tu petición: he alcanzado el número máximo de pasos permitido. Las operaciones que ya se hubieran hecho se mantienen; revisa el resumen antes de repetir la petición."), y todos los textos del modelo van al registro como intermedios. La segunda frase se añadió en el apply, tras la ejecución real con presupuesto bajo (ver Hallazgos del apply).
- La parada emite `RunStopped`.

### D11. Longitud máxima de la petición: constante, no configurable

`SalesAgent.MaxRequestLength = 2000`. `RunAsync` lanza `ArgumentException` si se supera, porque es un error del llamador. El validador del Runner usa la misma constante para `--prompt` (código 2). No se hace configurable: es parte del contrato de entrada, y un ajuste más solo añadiría una forma de desactivarla por error. Un test comprueba que las peticiones de todos los escenarios caben.

### D12. Runner: código 4 y salida

- `ExitCodes.StoppedByGuardrail = 4`.
- `AgentRunner` devuelve el resultado, y `RunnerHost` elige el código: 0 si es `Completed` y 4 si no. Con el 4 escribe por la salida de errores "La ejecución se detuvo por un guardrail: {motivo}".
- La salida estándar añade la línea del motivo de parada.

## Risks / Trade-offs

- [El presupuesto se supera en lo que consuma la última llamada] -> Documentado en la spec. Acotarlo exigiría limitar `max_tokens` por respuesta, fuera de alcance.
- [Las tools desconocidas no cuentan como errores seguidos ni aparecen en una ejecución parada por excepción] -> Un modelo que insiste en nombres inventados se para en `MaxModelCalls`. Con la parada por presupuesto se pierde la respuesta de la librería (D6) y la lista solo tendrá lo que vio el `FunctionInvoker`. Se documenta en el documento 05.
- [La auditoría basada en el registro no es duradera ni inalterable] -> Aceptable en un sandbox. El documento 05 y el ADR 0005 explican qué haría falta en un sistema real.
- [`IncludeScopes` cambia el formato de la consola] -> Solo afecta a la salida de errores, y la salida estándar no cambia de formato salvo la línea nueva. Las trazas citadas en los documentos 02 a 04 son anteriores y quedan como históricas.
- [**BREAKING** interno: `GetOrderQuery` y `CancelOrderCommand` cambian de forma] -> Solo los construyen las tools y los tests, que se actualizan en el change.
- [La misma clave en `PlaceOrder` y `ShipOrder` deja de dar conflicto (D3)] -> Ningún escenario lo exigía y `ShipOrder` no lo usa el agente. Si algún test lo comprobaba, se cambia citando D3.
- [`Terminate` con varias tools en una misma respuesta] -> Resuelto en el apply (ver Open Questions): corta también las pendientes de esa respuesta. La spec solo exige no volver a llamar al modelo, así que es compatible.

## Migration Plan

No hay datos persistentes ni despliegue: el estado es in-memory y se crea en cada ejecución. Sin sección `Guardrails`, los valores por defecto cubren con margen los escenarios existentes (3 y 4 llamadas). Para deshacerlo basta con revertir el PR.

## Open Questions

Ninguna pendiente. Resuelta en el apply:

- **`Terminate` con varias tools en una misma respuesta** (test de aprendizaje `GuardrailLearningTests.Terminate_OnTheFirstOfTwoCallsInOneResponse_…`, 2026-11-02): si el `FunctionInvoker` pone `Terminate` en la primera de dos peticiones de la misma respuesta, la segunda **no se ejecuta** y el modelo no recibe otra llamada. Para D7 significa que, al llegar al límite de errores seguidos, las tools que quedaban en esa respuesta no se ejecutan ni se registran.

`GuardrailLearningTests` fija también E1, E2, E5 y E6, para que una versión nueva de la librería que los cambie falle allí y no en el agente.

### Hallazgos del apply

- **`CallId` repetido** (observado, no documentado): si dos respuestas del modelo traen una petición con el mismo `CallId`, la librería da la segunda por respondida y termina el bucle con el motivo `Completed`. Apareció al reutilizar la misma instancia de un paso del guion en los tests; los tests generan ahora un paso nuevo por respuesta. Con un modelo real no debería ocurrir.
- **Spec de auditoría precisada**: `agent-guardrails` prohibía "argumentos de tools" en todos los eventos, pero `AccessDenied` debe llevar el identificador del pedido. Se precisó que la prohibición es para los eventos de invocación de tools, sin cambiar el comportamiento previsto.
- **Parada después de una escritura** (ejecución real contra DeepSeek, 2026-11-02): con `Guardrails:TokenBudget=2000`, el escenario feliz creó el pedido (`place_order` con éxito) y la parada por presupuesto llegó justo antes de la respuesta final. El texto fijo original ("No he podido completar tu petición…") daba a entender que no se había hecho nada. Se cambió para avisar de que lo ya hecho se mantiene y remitir al resumen, que lista las tools ejecutadas. La spec no cambia: sigue diciendo que la petición no se pudo terminar. Mejora posible, fuera de alcance: que el texto nombre las operaciones de escritura ya hechas.
- **El modelo real no pide tools que no se le ofrecen**: con la allow-list reducida y una petición que le pedía expresamente `place_order`, DeepSeek respondió en una llamada que no tenía esa tool. `BLOCKED_BY_POLICY` queda como defensa en profundidad, probada con el modelo falso.
- **La consola no mostraba los *scopes***: con `Configure<SimpleConsoleFormatterOptions>` y el nombre del formateador vacío, el proveedor de consola copia sus opciones heredadas sobre las del formateador. El test de configuración pasaba y la consola real no mostraba nada. Se usa `AddSimpleConsole`, que fija el formateador por nombre, y el test comprueba también ese nombre. Además, un `Dictionary` como *scope* se imprimía con su nombre de tipo; se usa `LoggerMessage.DefineScope` con plantilla ("Ejecución {RunId} del cliente {CustomerId}").
- **Arnés de los tests de aplicación**: como un cliente solo ve sus pedidos, el arnés lee y cancela cada pedido como su propietario, que busca entre los clientes conocidos con unas queries de auditoría nula, para no dejar accesos denegados falsos en la auditoría que comprueban los tests.
