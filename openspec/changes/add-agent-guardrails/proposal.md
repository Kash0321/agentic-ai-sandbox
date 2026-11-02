# Proposal

## Why

La fase 4 dejó un agente que actúa sobre el dominio de ventas con seis tools, pero sin más límites que los que trae por defecto `FunctionInvokingChatClient`, y con una limitación conocida (ADR 0004): `get_order` y `cancel_order` no comprueban que el pedido sea del cliente de sesión. Hoy cualquier cliente puede leer o **cancelar** el pedido de otro si conoce su número, y los números son secuenciales (`ORD-0001`, `ORD-0002`…).

Este change es el primero de los tres de la **fase 5** de `docs/plan.md` (*Guardrails*). Cubre la autorización por recurso, los límites del bucle y del coste, la allow-list de tools y la auditoría. Los otros dos son la confirmación humana de acciones destructivas y OpenTelemetry con Aspire Dashboard. El criterio de salida de la fase que le toca a este change: cada guardrail tiene tests sin LLM y las acciones bloqueadas devuelven un error tipado.

El explore (con un spike sobre `Microsoft.Extensions.AI` 10.11.0, recogido en `design.md`) encontró tres hechos que condicionan el alcance:

- **La idempotencia se salta la autorización.** Las claves de idempotencia son globales y la repetición se comprueba antes de ejecutar la operación. Otro cliente que repita una petición con la misma clave recibiría el resultado guardado del pedido ajeno, aunque se añadiera la comprobación de pertenencia.
- **`MaximumConsecutiveErrorsPerRequest` no protege de nada en este diseño**: solo cuenta las excepciones de las tools, y las nuestras nunca lanzan (devuelven `ToolError`). Un modelo que insiste en una tool que falla solo se para en el límite de iteraciones.
- **Una tool que no está en `ChatOptions.Tools` no pasa por el `FunctionInvoker`**: la librería responde al modelo con un texto propio, fuera del contrato `ToolError` y sin registro. Además, al llegar al límite de iteraciones, la librería hace una llamada extra sin tools: con un límite de N, el modelo recibe N+1 llamadas.

## What Changes

- **Autorización por recurso** (`Sandbox.Application`):
  - `GetOrder` y `CancelOrder` reciben el cliente que pide la operación. Si el pedido es de otro cliente, devuelven `ORDER_NOT_FOUND`, igual que si no existiera: no se confirma a un tercero que el pedido existe. **BREAKING** para quien construya `GetOrderQuery` o `CancelOrderCommand` (solo las tools y los tests).
  - Las claves de idempotencia se guardan **por ámbito**: el cliente en `PlaceOrder` y `CancelOrder`, y el almacén en `ShipOrder`. La misma clave usada por dos clientes son dos claves distintas.
  - Cada denegación deja un evento de auditoría con el motivo real ("pedido ajeno").
- **Guardrails del agente** (`Sandbox.Agents`), configurables en la sección `Guardrails` y validados al arrancar:
  - **Límite de llamadas al modelo** por ejecución (por defecto, 10). Nunca se hace una llamada de más, tampoco la extra de la librería.
  - **Límite de errores de tool seguidos** (por defecto, 3), contado sobre los `ToolError` y no sobre excepciones.
  - **Presupuesto de tokens** por ejecución (por defecto, 20.000 de entrada y salida): no se inicia una llamada si el acumulado lo alcanza.
  - **Allow-list de tools** (por defecto, las seis). Solo se ofrecen al modelo las permitidas. Si el modelo pide igualmente una que existe pero no está permitida, recibe `BLOCKED_BY_POLICY` y no se ejecuta. Un nombre inventado queda como `UNKNOWN_TOOL` en el resultado y en la auditoría.
  - **Longitud máxima de la petición** (2.000 caracteres), como validación de entrada.
- **Motivo de parada**: cada ejecución termina con un motivo (`Completed`, `ModelCallLimit`, `ToolErrorLimit`, `TokenBudget`). Si para un guardrail, la respuesta final es un texto fijo del sistema que lo explica, no un texto del modelo.
- **Auditoría estructurada**: eventos con identificador estable en una categoría de registro propia, correlacionados por un identificador de ejecución y el cliente de sesión. Registran la tool, la decisión (permitida, bloqueada, desconocida), el resultado y las denegaciones, sin argumentos, resultados ni secretos.
- **Runner**:
  - código de salida nuevo, el **4**, cuando la ejecución la para un guardrail;
  - la salida muestra el motivo de parada;
  - escenario nuevo `read-only`: la petición del escenario feliz con solo las tools de lectura permitidas;
  - el registro de consola muestra el identificador de ejecución.
- **Tests**: cada guardrail con tests sin LLM (modelo falso con guion), la autorización y la idempotencia por ámbito en la aplicación, y los escenarios de extremo a extremo.
- **Documentación**:
  - documentos 05 (guardrails implementados y hallazgos del spike) y 06 (autorización: `ORDER_NOT_FOUND` en lugar de `FORBIDDEN`, y la lista de escenarios previstos para la fase 6);
  - **ADR 0005** sobre la autorización por recurso: dónde se decide, por qué no se revela la existencia del pedido y por qué las claves de idempotencia van por ámbito;
  - ADR 0004 (la limitación conocida queda resuelta), glosario, índice y backlog, `docs/plan.md` y `README.md`.

### Supuestos

Los valores por defecto (10 llamadas, 3 errores seguidos, 20.000 tokens y 2.000 caracteres) se fijan a partir de lo medido en la fase 4: el escenario feliz usa 3 llamadas y unos 4.500 tokens, y el de la respuesta perdida, 4 llamadas. Son configurables, salvo la longitud máxima.

### Fuera de alcance

- **Confirmación humana** de `cancel_order` (`CONFIRMATION_REQUIRED`, `ApprovalRequiredAIFunction`): segundo change de la fase 5.
- **OpenTelemetry y Aspire Dashboard**: tercer change de la fase 5. La auditoría de este change usa `ILogger`, que OpenTelemetry podrá exportar después.
- **Guardrails probabilísticos** (detección de *prompt injection*, LLM como juez) y guardrails de salida: fase 6 o posteriores.
- **Escenarios de ataque** (pedido ajeno pedido por el modelo, inyección en un campo de texto): matriz de la fase 6. Aquí se prueban con tests.
- **Límite de tasa** de acciones de escritura y *circuit breaker* del proveedor.
- **Cambios en el `Dockerfile`, en compose o en la CI**: sin argumentos, el Runner sigue haciendo el saludo, y el check `image` sigue esperando el código 2.

## Capabilities

### New Capabilities

- `agent-guardrails`: los guardrails deterministas del bucle del agente:
  - su configuración y su validación;
  - el límite de llamadas al modelo, el de errores de tool seguidos y el presupuesto de tokens;
  - la allow-list, con `BLOCKED_BY_POLICY` y las tools desconocidas;
  - el motivo de parada;
  - la longitud máxima de la petición;
  - la auditoría estructurada y su correlación.

### Modified Capabilities

- `order-operations`:
  - "Commands CancelOrder y ShipOrder" y "Queries de solo lectura": `CancelOrder` y `GetOrder` reciben el cliente que pide y tratan el pedido ajeno como inexistente;
  - requisitos nuevos: ámbito de las claves de idempotencia y auditoría de los accesos denegados.
- `agent-tools`:
  - "Catálogo de tools expuestas": al modelo se ofrecen las permitidas por la allow-list;
  - "Tool get_order" y "Tool cancel_order": un pedido de otro cliente da `ORDER_NOT_FOUND`;
  - "Registro de cada invocación": pasa a ser un evento de auditoría con la decisión y la correlación.
- `sales-agent`:
  - "Bucle de invocación de tools": el bucle también termina cuando lo para un guardrail;
  - "Resultado de una ejecución": incluye el motivo de parada;
  - "Escenarios con nombre": escenario `read-only` y allow-list por escenario.
- `runner-host`:
  - "Códigos de salida": el 4;
  - "Salida de una ejecución del agente": el motivo de parada;
  - "Argumentos de ejecución inválidos": la petición demasiado larga.

## Impact

- **Ficheros nuevos**:
  - en `src/Sandbox.Agents/`: las opciones y el validador de los guardrails, el cliente que limita llamadas y tokens, el motivo de parada y los eventos de auditoría del agente;
  - en `src/Sandbox.Application/`: los eventos de auditoría de la aplicación;
  - tests nuevos en `Sandbox.Agents.Tests`, `Sandbox.Application.Tests` y `Sandbox.Scenarios.Tests`;
  - `docs/adr/0005-…`.
- **Ficheros modificados**:
  - `GetOrderQuery`, `CancelOrderCommand`, `SalesCommands`, `SalesQueries` e `IdempotencyStore`;
  - `SalesTools`, `SalesToolNames`, `ToolErrorCodes`, `SalesAgent`, `AgentRunResult` y `CallCountingChatClient`;
  - `RunnerHost`, `ExitCodes`, `AgentRunner`, `AgentOptionsValidator`, `AgentScenario`, `ScenarioCatalog` y `appsettings.json`;
  - el registro de pruebas (`RecordingLoggerProvider`), para que capture los *scopes*;
  - `docs/learn/05-…`, `06-…`, `README.md` y `glosario.md`, `docs/adr/0004-…`, `docs/plan.md` y `README.md` raíz.
- **Dependencias**: ninguna nueva.
- **Comportamiento visible**:
  - sin argumentos, nada cambia;
  - con `--scenario` o `--prompt`, el registro de consola muestra el identificador de ejecución, y una ejecución que para un guardrail termina con el código 4;
  - un cliente ya no puede ver ni cancelar pedidos de otro.
- **Conceptos nuevos y dónde se documentan**:
  - documento 05: guardrails de bucle y de presupuesto, allow-list con tools no ofrecidas, *fail closed*, motivo de parada, auditoría correlacionada, y lo que los límites de la librería cubren y lo que no;
  - documento 06 y ADR 0005: autorización por recurso en la aplicación, no revelar la existencia de un recurso ajeno (enumeración) y ámbito de las claves de idempotencia;
  - glosario: los términos nuevos (presupuesto de tokens, motivo de parada, enumeración de recursos, ámbito de idempotencia, correlación).
