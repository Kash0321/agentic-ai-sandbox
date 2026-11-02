# Design

## Context

Motivación y alcance en `proposal.md`. Requisitos en `specs/agent-tools`, `specs/sales-agent` y en los deltas de `order-operations`, `fault-injection` y `runner-host`.

Estado de partida observado el 2026-11-02, en la rama `feat/add-sales-agent` creada desde `main` (`f061b17`):

- **`Sandbox.Agents`** solo tiene el cliente de chat:
  - `DeepSeekChatClientFactory`, con los decoradores de traducción de errores y de registro de uso;
  - `ThinkingSwitch`, que desactiva el razonamiento con `JsonPatch` vía `ConfigureOptions`.

  No hay tools ni bucle.
- **`Sandbox.Runner`**:
  - `RunnerHost` compone el host y traduce excepciones a códigos de salida, incluido el `AggregateException` de varias opciones inválidas;
  - `GreetingRunner` hace el saludo.
- **`Sandbox.Application`**:
  - `ISalesCommands` e `ISalesQueries`, decorados con el inyector de fallos;
  - no hay ninguna query que liste productos;
  - `FaultOperations.Known` enumera las operaciones válidas del inyector.
- **Tests**:
  - `FakeChatClient` solo devuelve un texto fijo o lanza una excepción;
  - se enlaza en `Sandbox.Scenarios.Tests` junto con `RecordingLoggerProvider`.
- **compose**: el servicio `runner` no fija `command`, así que `docker compose run --rm runner --scenario <id>` pasa los argumentos al Runner sin cambios.

### Explore previo: experimentos y resultados

El explore se hizo entre el 2026-11-01 y el 2026-11-02, con decisiones del responsable mediante preguntas interactivas. Los experimentos usaron una consola desechable en el scratchpad (`spike-fase4`), con referencias a los proyectos del repositorio sin modificarlos, el SDK 10.0.403, `Microsoft.Extensions.AI` 10.11.0 y `deepseek-flash`. La clave se leyó del fichero dentro del proceso y nunca se imprimió. Hubo 18 llamadas reales.

**Decisiones del responsable:**

| Tema | Decisión |
|---|---|
| Entrada del Runner | `--scenario <id>` y `--prompt "texto"`; una petición por ejecución |
| Identidad | Cliente de sesión fijado fuera del modelo; las tools no reciben `customer_id` |
| Clave de idempotencia | La pone el modelo, como parámetro descrito |
| Descubrir productos | Query nueva `ListProducts` y su tool |
| Tools | Seis, sin `ship_order` |
| Razonamiento con tools | Investigarlo; resultado: sin soporte en esta fase, con aviso y documentado |
| Trazas | Registro por tool (nombre, duración y resultado, sin argumentos) y resumen final |
| Salida | Respuesta final y resumen por la salida estándar; textos intermedios al registro |
| Escenarios | `happy-order` y `lost-response` |
| Changes | Uno |

**Consultas a fuentes primarias:**

| Qué | Resultado | Fuente |
|---|---|---|
| Razonamiento con tools | "the `reasoning_content` must be fully passed back to the API in all subsequent requests"; si falta, "the API will return a 400 error". Sin tools, se ignora. Activado por defecto | `api-docs.deepseek.com/guides/thinking_mode`, consultada el 2026-11-01 (sin fecha de versión; ejemplos de 2026-04-19) |
| Tipos de `Microsoft.Extensions.AI` 10.11.0 | Existen `DelegatingAIFunction` y `FunctionInvocationContext`. `FunctionInvokingChatClient` tiene `FunctionInvoker`, `IncludeDetailedErrors`, `AllowConcurrentInvocation`, `MaximumIterationsPerRequest`, `MaximumConsecutiveErrorsPerRequest`, `AdditionalTools`, `TerminateOnUnknownCalls` y `CurrentContext` | Reflexión sobre los ensamblados |

**Experimentos sin red:**

| Experimento | Observado |
|---|---|
| Nombre por defecto con `AIFunctionFactory.Create(método)` | El nombre del método sin `Async`: `PlaceOrderAsync` da `PlaceOrder`. Con `AIFunctionFactoryOptions.Name` queda el indicado (`place_order`) |
| Esquema de parámetros | El `CancellationToken` no aparece y los parámetros sin valor por defecto son `required`. Una lista de `record` da `array` de objetos. `[property: Description]` en un `record` posicional pone la descripción en el esquema |
| Serialización del resultado | camelCase, enumerados como texto (`"status":"Placed"`), sin propiedades nulas (un éxito no lleva `error`, un fallo no lleva `data`). Los caracteres no ASCII se escapan (`ó`, `+`). `InvokeAsync` devuelve un `JsonElement`, no el objeto |
| Tokens acumulados | Con dos turnos de 100 y 200 tokens de entrada, la respuesta de `FunctionInvokingChatClient` da 300: los acumula entre iteraciones. El número de llamadas no viene en la respuesta |
| Argumento obligatorio ausente | `ArgumentException` antes de ejecutar la tool. El modelo recibe solo `"Error: Function failed."`. El `ParamName` es `arguments`, y el parámetro solo aparece en el mensaje interno, en inglés |

**Experimentos con DeepSeek (`deepseek-flash`):**

| Experimento | Observado |
|---|---|
| Caso feliz ("quiero pedir dos tazas rojas"), sin razonamiento | `ListProducts`, después `PlaceOrder`, después texto: 3 llamadas, 3.607 tokens de entrada y 188 de salida. Encontró `SKU-MUG-RED` solo |
| Respuesta perdida (`PlaceOrder` con fallo `After` en la primera llamada) | **2 de 2** ejecuciones reintentaron con la **misma clave** (`tazas-rojas-2-001`, `mugs-rojas-ana-001`) y las mismas líneas. Un solo pedido y 18 disponibles. Fueron 4 y 5 llamadas, con 5.113 y 6.716 tokens de entrada. Una vez llamó también a `GetCustomer` sin necesidad |
| Claves que inventa el modelo | Semánticas y a veces con fechas antiguas (`mug-red-2-20250214-01`). Todas válidas (≤ 64 caracteres) |
| `response.Text` | Concatena el texto intermedio de cada turno ("Voy a buscar…") con la respuesta final |
| Varias tools en una respuesta | En una ejecución pidió `ListProducts` y `GetCustomer` a la vez; se ejecutaron las dos |
| Razonamiento con tools (`Thinking=true`) | Sin error. El historial guardó un `TextReasoningContent` (283 caracteres en un turno). Capturando el cuerpo de las peticiones: los mensajes del asistente llevan `role`, `content` y `tool_calls`, **sin `reasoning_content`**, y DeepSeek respondió **HTTP 200** en las 3 rondas. Contradice la documentación |
| Nombres en las descripciones | La descripción citaba `list_products` y la tool se llamaba `ListProducts`; el modelo acertó igualmente. Es un riesgo innecesario |

**Ejecuciones reales del Runner en el apply (tarea 6.1, 2026-11-02, `dotnet run` con la clave en User Secrets, 11 llamadas):**

| Ejecución | Resultado |
|---|---|
| `--scenario happy-order` | Código 0. `list_products` (OK) y `place_order` (OK) en 3 llamadas, con 4.462 tokens de entrada y 164 de salida (1.212, 1.492 y 1.758 de entrada por llamada). Respuesta con `ORD-0001`, 2 tazas y 25,00 €. Hay más tokens que en el spike (3.607) porque el system prompt y las descripciones son más largos |
| `--scenario lost-response`, 2 ejecuciones | Código 0 en las dos. `list_products` (OK), `place_order` (`SERVICE_UNAVAILABLE`) y `place_order` (OK) en 4 llamadas, con 6.348 y 6.388 tokens de entrada. Las dos respuestas citan **`ORD-0001`**: con una clave distinta, el reintento habría creado `ORD-0002`, así que el modelo **reutilizó la clave** las dos veces. Textos intermedios registrados, como "El servicio falló temporalmente. Reintento con la misma clave." |

Con las del spike, el modelo ha reutilizado la clave en 4 de 4 respuestas perdidas.

**Verificación en contenedor (tarea 6.2, 2026-11-02, Docker 29.9.2):**

- `docker build --target test .`: 247 tests de los cuatro proyectos en verde, en 265 s.
- Imagen final endurecida, sin red ni clave: código 2 ("DeepSeek:ApiKey es obligatoria."). Con `--scenario no-existe`, también código 2, nombrando las dos opciones.
- `docker compose run --rm runner --scenario happy-order`, con la clave como secreto por fichero (`DEEPSEEK_API_KEY_FILE`): código 0, 3 llamadas y 4.348 tokens de entrada. Compose no cambió: los argumentos llegan al `ENTRYPOINT`.
- Canarios en `secrets/` y `.env`: ninguno en la imagen de test (`grep` con control positivo) ni en el sistema de ficheros exportado de la final.

**Suposiciones sin verificar** (se comprueban en las tareas):

- Que `FunctionInvokingChatClient.FunctionInvoker` reciba el contexto de cada invocación antes de asignar los argumentos, de modo que el `ArgumentException` de un argumento ausente se pueda capturar ahí. **Confirmado en el apply (tarea 2.1)**: el `ArgumentException` salta dentro de `context.Function.InvokeAsync`, en el `FunctionInvoker`, y el cuerpo de la tool no llega a ejecutarse (`FunctionInvokingChatClientLearningTests`).
- Que `FunctionInvokingChatClient` propague la `OperationCanceledException` de una tool, en lugar de convertirla en un error para el modelo. **Matizado en el apply (tarea 2.1)**: la propaga solo si **el token de la petición está cancelado**. Una `OperationCanceledException` lanzada con el token sin cancelar se trata como un fallo de la tool, y el modelo recibe un error y continúa. Por tanto, el borde interior no debe convertir la cancelación en `INTERNAL_ERROR`: la deja pasar y el bucle decide.
- Que un segundo proveedor de línea de comandos con *switch mappings* (`--scenario` → `Agent:Scenario`) conviva con el que añade `Host.CreateApplicationBuilder`. **Confirmado en el apply (tarea 5.1)**: con `--scenario happy-order --customer CUST-002`, el agente recibe la petición del escenario y el cliente indicado (`RunnerAgentTests`). El proveedor por defecto también lee esos argumentos, pero como claves de raíz (`scenario`) que nadie usa.
- Que el codificador relajado de `SerializerOptions` se aplique a los resultados, y si se aplica también al esquema que se envía al proveedor. **Comprobado en el apply (tarea 2.3)**: los resultados salen compactos y sin escapar (`"name":"Lámpara de escritorio"`). El esquema generado **sigue escapado** (`"Líneas del pedido…"`), porque su generación no usa ese codificador. Se acepta: las descripciones se envían una vez por llamada, y los resultados se acumulan turno a turno.

## Goals / Non-Goals

**Goals:**

- Que las tools sean finas: una línea de traducción por operación, sin reglas de negocio, con todo lo transversal (errores, registro, argumentos inválidos) en un único borde.
- Que todo el bucle se pruebe sin red y de forma determinista, con un modelo falso con guion.
- Que el contrato de las tools no tenga que cambiar en la fase 5: la identidad ya está fuera de él.

**Non-Goals:**

- Un framework de agentes (`Microsoft.Agents.AI`, Semantic Kernel): basta `IChatClient` con `UseFunctionInvocation`.
- Abstraer el prompt en plantillas o versionarlo: es un recurso de texto.
- Medir la calidad del agente con varios modelos o con el razonamiento activado.

## Decisions

### D1. Reparto entre capas

| Pieza | Proyecto | Por qué |
|---|---|---|
| Query `ListProducts` y operación del inyector | `Sandbox.Application` | Es una operación de lectura más; el agente es solo su primer consumidor |
| `ToolResult<T>`, `ToolError`, códigos propios de las tools | `Sandbox.Agents` | Es el contrato con el LLM; el dominio no lo conoce (ADR 0003) |
| Tools, borde de invocación, agente, system prompt | `Sandbox.Agents` | Invocación del LLM (`AGENTS.md`, principio 1) |
| Opciones de ejecución, catálogo de escenarios, salida | `Sandbox.Runner` | Raíz de composición y forma de uso. Los escenarios combinan agente, cliente y fallos inducidos |

`LayerRulesTests` sigue comprobando que el dominio y la aplicación no ven librerías de IA.

### D2. Contrato de resultado

```
ToolResult<T> = (bool Success, T? Data, ToolError? Error)
ToolError     = (string Code, string Message, bool Retryable)
Retryable     = Kind == Transient                // SERVICE_UNAVAILABLE
```

- La forma es la de `AGENTS.md`, sección 4.4.
- La traducción desde `Result<T>` es directa: `Code` y `Message` del `DomainError`, y `Retryable` a partir de su `ErrorKind`. Así se cumple lo que el ADR 0003 dejó preparado para esta fase.
- Los códigos que nacen en la capa de tools (`INTERNAL_ERROR`, `INVALID_ARGUMENTS`) van en una clase propia de `Sandbox.Agents`, no en `ErrorCodes` del dominio, porque el dominio nunca los produce.

### D3. Un borde en dos niveles

```
FunctionInvokingChatClient
  FunctionInvoker (borde exterior, uno para todas las tools)
     |  mide la duración, registra nombre, duración y resultado (lee success/error.code del JSON)
     |  ArgumentException al asignar argumentos  --> ToolResult INVALID_ARGUMENTS
     |  OperationCanceledException                 --> se propaga
     v
  AIFunction (AIFunctionFactory sobre un método de SalesTools)
     |
  método de la tool (borde interior)
     |  await operación de aplicación --> ToolResult.From(result)
     |  excepción inesperada --> registra el detalle y devuelve INTERNAL_ERROR
```

- **Interior**: cada método captura lo inesperado de su propio cuerpo. Por eso cualquier excepción que llegue al borde exterior solo puede venir de la asignación de argumentos, y se puede traducir a `INVALID_ARGUMENTS` sin ambigüedad.
- **Exterior**: el `FunctionInvoker` es el único sitio que conoce nombre, duración y resultado de **todas** las invocaciones, también las que fallan antes de ejecutar. Se usa para el registro y para producir el resultado del resumen.
- **Mensaje de `INVALID_ARGUMENTS`**: nombra la tool y pide revisar los parámetros obligatorios y sus tipos. No copia el mensaje de la excepción, que es interno y en inglés.

Alternativas:
- `DelegatingAIFunction` por tool: hay que envolver cada una, y no ve los fallos del bucle.
- Dejar el comportamiento por defecto: el modelo recibiría "Error: Function failed." sin código, lo que contradice `AGENTS.md` 4.4.

### D4. Nombres, parámetros y descripciones

- **Nombres** explícitos en `snake_case` (`AIFunctionFactoryOptions.Name`): `list_products`, `get_stock`, `get_customer`, `get_order`, `place_order` y `cancel_order`. Las descripciones y el prompt los citan, y un test comprueba que coinciden.
- **Parámetros** con nombres de C# en camelCase, que son los que genera el esquema: `sku`, `orderId`, `lines` (`sku`, `quantity`) e `idempotencyKey`.
- **Descripciones** en español, escritas para el modelo:
  - qué hace la tool y cuándo usarla;
  - formatos (`ORD-0001`) y unidades (EUR);
  - en la clave, "nueva por petición distinta, la misma al reintentar".

  Las líneas usan un `record` propio de `Sandbox.Agents` con `[property: Description]`, no el `OrderLineRequest` del dominio, para no poner atributos de presentación en el dominio.

### D5. Serialización

`AIFunctionFactoryOptions.SerializerOptions` parte de `AIJsonUtilities.DefaultOptions`, que da camelCase, enumerados como texto y omite los nulos (comprobado en el spike), y añade `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`:

- los mensajes y los nombres con acentos llegan legibles (`Lámpara`, no `Lámpara`) y gastan menos tokens;
- "Unsafe" se refiere a incrustar el JSON en HTML, que aquí no ocurre.

Si el esquema que se envía al proveedor sigue escapado, se documenta: lo importante son los resultados, que se repiten en cada turno.

**Observado en el apply (tarea 2.3)**: `AIJsonUtilities.DefaultOptions` serializa **indentado** (`WriteIndented`), con saltos de línea y espacios que son tokens repetidos en cada turno. En el spike no se vio porque el resultado se volvía a serializar con las opciones por defecto de `System.Text.Json`, que son compactas. Las opciones de las tools fijan `WriteIndented = false`.

### D6. Cliente de sesión (ADR 0004)

- Un objeto de sesión con el identificador del cliente, registrado como singleton a partir de las opciones del Runner. Las tools lo reciben por constructor; `get_customer` y `place_order` lo usan, y ninguna tool acepta un cliente como parámetro.
- **Trasciende este change**: la fase 5 construirá sobre él la autorización por recurso (`FORBIDDEN`) y los guardrails. Por eso se registra como `docs/adr/0004-identidad-del-cliente-fuera-del-modelo.md`.

Alternativa: el parámetro `customer_id`. Es un vector de *prompt injection* ("pídelo a nombre de CUST-002") y obligaría a cambiar el contrato en la fase 5.

### D7. El agente

```
SalesAgent.RunAsync(peticion, ct)
  mensajes = [system: prompt incrustado, user: peticion]
  pipeline = chat base (DeepSeek o falso)
               <- contador de llamadas (IChatClient propio que reenvía)
               <- FunctionInvokingChatClient (FunctionInvoker = borde exterior)
  respuesta = pipeline.GetResponseAsync(mensajes, Tools = las seis)
  final     = texto del último mensaje del asistente
  intermedios (los demás textos del asistente) --> registro, nivel Information
  resultado = (final, llamadas, tools [(nombre, resultado)], tokens de respuesta.Usage)
```

- **El pipeline se construye por ejecución**: el contador y la lista de tools invocadas son de cada ejecución, no compartidos.
- **Tokens**: salen de `response.Usage`, que el bucle acumula (comprobado). **Llamadas**: las cuenta un `IChatClient` propio que reenvía cada llamada al cliente base.
  - **Ajustado en el apply (tarea 4.3)**: el borrador lo planteaba como un `DelegatingChatClient`, pero esa clase libera el cliente interior al liberarse. El analizador CA2215 obliga a llamar a `base.Dispose`, así que habría liberado el cliente de chat compartido por todas las ejecuciones. Por eso `CallCountingChatClient` implementa `IChatClient` directamente y su `Dispose` no hace nada.
- **Límites del bucle**: `MaximumIterationsPerRequest` y `MaximumConsecutiveErrorsPerRequest` quedan en sus valores por defecto (40 y 3). Ajustarlos es un guardrail de la fase 5.
- **Tools en serie**: `AllowConcurrentInvocation` se fija a `false` de forma explícita, aunque ya es su valor por defecto, porque la spec `sales-agent` exige ejecutar en serie las tools de una misma respuesta. Así, un cambio del valor por defecto en una versión futura de la librería no cambia el comportamiento. Lo añadió la verificación del change.
- **Prompt**: un fichero de texto como recurso incrustado de `Sandbox.Agents`, que se puede leer y revisar como texto. Un test comprueba lo que exige la spec: menciona la clave y no contiene los límites del dominio.
- **Aviso de razonamiento**: si `DeepSeekOptions.Thinking` es `true`, se registra un `Warning` al empezar la ejecución. Cita la documentación de DeepSeek y lo observado en el explore.

Alternativa: `Microsoft.Agents.AI` (`ChatClientAgent`). Añade una dependencia y una capa más para algo que hoy son unas 50 líneas, y oculta el bucle que la fase quiere enseñar.

### D8. Runner: opciones, escenarios y salida

- **Opciones** (sección `Agent`): `Scenario`, `Prompt` y `CustomerId`. `--scenario`, `--prompt` y `--customer` se mapean a ellas con un proveedor de línea de comandos con *switch mappings* (suposición pendiente).
- **Validador**, con `ValidateOnStart`, que rechaza:
  - un escenario desconocido;
  - escenario y texto a la vez;
  - un texto vacío o de solo espacios;
  - un cliente vacío si se indica.

  Los mensajes nombran `Agent:Scenario`, `Agent:Prompt` o `Agent:CustomerId`. Un error aquí más otro de DeepSeek llegan juntos en el `AggregateException` que ya traduce `RunAsync`.
- **Catálogo de escenarios** en el Runner. Cada escenario tiene identificador, cliente, petición y reglas del inyector:
  - `happy-order`: `CUST-001`, "Hola, quiero pedir dos tazas rojas, por favor.", sin reglas;
  - `lost-response`: la misma petición, con `PlaceOrder`, `FirstCalls` 1 y `After`.

  Las reglas del escenario se **añaden** a las configuradas con un `PostConfigure<FaultInjectionOptions>`. Así no se pisan los índices `Rules:n` de la configuración, y el validador del inyector las revisa igual.
- **Cliente de sesión**: el de `--customer`; si no hay, el del escenario; si tampoco, `CUST-001`.
- **Despacho**: con escenario o texto, un `AgentRunner` ejecuta el agente y escribe por la salida estándar la respuesta final y el resumen. Sin ninguno de los dos, `GreetingRunner` como hasta ahora, de modo que la CI y el check `image` no cambian.

### D9. Tests sin red

- **`ScriptedChatClient`** (`Sandbox.Agents.Tests/Fakes`, enlazado en `Sandbox.Scenarios.Tests`). Es un guion de pasos, y cada paso devuelve llamadas a tools (nombre y argumentos) o texto, con sus tokens. Guarda los mensajes recibidos, así que un test puede comprobar el `FunctionResultContent` que llegó al modelo. `FakeChatClient` se mantiene para el saludo.
- **Tests de tools**:
  - esquema y descripciones de cada una (`AGENTS.md` 4.3 y 4.10);
  - caso feliz;
  - cada código de error de la spec;
  - `INVALID_ARGUMENTS`, `INTERNAL_ERROR` (con una operación que lanza) y la cancelación.
- **Tests del agente**:
  - bucle de tres llamadas;
  - varias tools por respuesta;
  - textos intermedios;
  - tokens acumulados;
  - prompt;
  - aviso de razonamiento.
- **Tests del Runner**:
  - los dos escenarios y `--prompt` con `--customer`, de extremo a extremo con el host real y el cliente con guion;
  - cada argumento inválido (código 2);
  - un error del proveedor (código 3).

### D10. Documentación

- **Documento nuevo `docs/learn/12-diseno-de-tools-para-llms.md`**: nombres y descripciones, granularidad, esquemas, salidas compactas, errores accionables y `Retryable`, el borde de invocación, la idempotencia en manos del modelo (con lo observado) y la identidad fuera del contrato.
- **Documentos 02 y 03**, con las trazas reales del spike: llamadas, tokens por turno, llamadas en paralelo y textos intermedios.
- **Documento 04**, con el razonamiento y las tools.
- **ADR 0004**, el glosario, el índice, `docs/plan.md` y el `README.md` raíz (uso de `--scenario` y `--prompt`).

## Risks / Trade-offs

- [DeepSeek empieza a exigir `reasoning_content` con tools, como dice su documentación] → El razonamiento está desactivado por defecto, y activarlo registra un aviso. Si un día da 400, el error llega como `ChatProviderException` (código 3), y el arreglo es un decorador con `JsonPatch` en un change propio.
- [El modelo no reutiliza la clave al reintentar] → El spike lo vio en 2 de 2 ejecuciones, que no es una garantía. Si cambia, el efecto es un pedido duplicado, visible en el resumen y en el estado. El dominio no puede impedirlo sin la clave. La fase 6 lo medirá con más ejecuciones.
- [El modelo llama a tools innecesarias (`get_customer`)] → Cuesta tokens, no corrección. Las descripciones dicen cuándo usar cada tool.
- [Coste por ejecución: 3.600–6.700 tokens de entrada; unos 300 por llamada son de las descripciones] → Se muestra en el resumen. Los presupuestos de tokens son de la fase 5.
- [`get_order` devuelve pedidos de otros clientes] → Es conocido y está fuera de alcance (fase 5, `FORBIDDEN`). Se documenta en el documento 12 y en el ADR 0004.
- [Los textos intermedios del modelo van al registro] → Son salida del modelo, no secretos ni argumentos, y se registran con nivel `Information`. `AGENTS.md` prohíbe los *payloads* completos por defecto: no se registran los mensajes enviados ni los resultados de las tools.
- [El esquema de las tools sigue llevando escapes `ó`] → Coste menor. Se mide y se documenta (D5).

## Migration Plan

Sin datos ni consumidores. Sin argumentos el Runner hace lo mismo que antes, y la CI no cambia. Para revertir basta con quitar el change.

## Open Questions

- ¿Hará falta en la fase 6 un paso del guion falso que simule un error del proveedor a mitad del bucle? Se decidirá con la matriz de escenarios; no cambia este diseño.
