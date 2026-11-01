# Proposal

## Why

Las fases 1 y 2 dejaron una solución que se construye, se prueba y habla con DeepSeek, pero `Sandbox.Domain` y `Sandbox.Application` siguen vacíos. La fase 4 necesita algo sobre lo que poner tools. Las tools serán finas y sin lógica, así que el dominio tiene que aportar desde ya:

- las reglas de negocio;
- una máquina de estados explícita;
- errores con códigos estables;
- idempotencia frente a los reintentos del modelo;
- fallos inducidos de forma reproducible.

Es la **fase 3** de `docs/plan.md` (*Servicios dummy*), en un único change. Su criterio de salida: tests de dominio, incluidos fallos y transiciones inválidas.

El change parte de un explore cuyos experimentos y decisiones se recogen en `design.md`. Hallazgos que condicionan el alcance:

- **Una respuesta perdida tras un command que sí se ejecutó es el caso que más importa.** Un fallo inducido *después* de ejecutar hace que el agente reintente algo que ya ocurrió. Por eso los tres commands llevan clave de idempotencia, no solo el de creación.
- **`Random` con semilla no es reproducible entre versiones mayores de .NET ni es *thread-safe*** (documentación oficial, comprobado en el explore). El inyector se diseña para que los tests no dependan de una secuencia concreta.
- **`FunctionInvokingChatClient` ejecuta las tools en serie por defecto** en `Microsoft.Extensions.AI` 10.11.0. La concurrencia no es un problema inmediato, pero el estado in-memory se protege igualmente.

## What Changes

- **Dominio** (`Sandbox.Domain`):
  - pedidos (`Order`) con máquina de estados `Placed -> Shipped` y `Placed -> Cancelled`;
  - clientes (`Customer`, activo o bloqueado);
  - stock por producto (existencias y reservas);
  - reglas: pedido vacío, cantidad fuera de rango, línea duplicada, cliente bloqueado, importe máximo, stock insuficiente y transición inválida;
  - errores como resultado (`Result<T>` con un `DomainError` que lleva código estable y tipo), sin excepciones de negocio.
- **Operaciones** (`Sandbox.Application`), con CQRS simplificado:
  - **commands** `PlaceOrder` (reserva stock), `CancelOrder` (libera stock) y `ShipOrder` (consume stock), los tres con **clave de idempotencia**;
  - **queries** `GetOrder`, `GetCustomer` y `GetStock`, con vistas compactas;
  - estado in-memory con un catálogo semilla fijo, identificadores secuenciales y reloj inyectado (`TimeProvider`).
- **Inyector de fallos configurable** (`Sandbox.Application`): decorador por operación. Puede dar latencia y el error transitorio `SERVICE_UNAVAILABLE` antes o después de ejecutar. Se dispara siempre, en las N primeras llamadas o con una probabilidad y una semilla. Está desactivado si no hay reglas.
- **Runner**: registra los servicios de aplicación y valida las opciones del inyector al arrancar. Si son inválidas, termina con el código 2 (configuración inválida). El saludo de la fase 2 no cambia.
- **Tests**: nuevo proyecto `tests/Sandbox.Application.Tests`. `Sandbox.Domain.Tests` sustituye su test de cableado por tests reales.
- **Contenedor**: el `Dockerfile` copia el nuevo proyecto de tests en la etapa de restauración.
- **Documentación**:
  - documento nuevo `docs/learn/11-dominios-aptos-para-agentes.md`;
  - ADR 0003 sobre los errores de dominio como resultados;
  - glosario, índice y backlog de `docs/learn/README.md`;
  - `docs/plan.md`: estado de la fase 3 y corrección de la fila de la fase 2, que sigue como "pendiente de fusionar".

### Fuera de alcance

- Tools (`AIFunction`), `ToolResult<T>`, system prompt y cambios en el bucle del Runner: fase 4. Aquí el Runner solo registra los servicios.
- Autorización por recurso (`FORBIDDEN`), contexto de llamada e identidad: fase 5. `Order` ya guarda el `CustomerId`, pero nadie lo comprueba todavía.
- Guardrails (confirmación de acciones destructivas, límites, auditoría): fase 5.
- **Sagas multipaso y compensaciones de varios servicios**: se descartan por ahora, pero se quiere volver a ellas más adelante con un dominio algo más rico. Cancelar un pedido y liberar su stock es la única compensación de este change.
- Estados adicionales (`Confirmed`, `Delivered`), modificar líneas de un pedido, pagos, crédito de clientes y varias divisas.
- Persistencia real: el estado vive en memoria y se pierde al terminar el proceso.
- Cargar el catálogo desde configuración: es fijo en código, y los tests construyen su propio estado.
- Un error `TIMEOUT` distinto de `SERVICE_UNAVAILABLE`, *timeouts* reales y política de reintentos: fase 5.

## Capabilities

### New Capabilities

- `order-domain`: reglas de negocio del dominio de ventas. Cubre:
  - pedidos, clientes y stock;
  - la máquina de estados del pedido;
  - las validaciones y su orden de evaluación;
  - el modelo de error (código estable y tipo) sin excepciones de negocio.
- `order-operations`: commands y queries que expone la capa de aplicación sobre el dominio. Cubre:
  - el contrato de cada operación;
  - la idempotencia de los commands;
  - las vistas compactas;
  - el catálogo semilla;
  - el determinismo (reloj inyectado e identificadores secuenciales).
- `fault-injection`: fallos y latencia inducidos de forma configurable y reproducible en las operaciones de aplicación. Cubre los disparadores, el momento (antes o después de ejecutar), el error transitorio, la validación de la configuración y la desactivación por defecto.

### Modified Capabilities

- `solution-layout`, "Proyectos de tests por capa": se añade `Sandbox.Application.Tests`.
- `container-build`, "Objetivo de tests en contenedor": la etapa de tests ejecuta los tests de todos los proyectos de tests, ya no de tres.
- `runner-host`: nuevo requisito para que el Runner registre los servicios de aplicación y rechace al arrancar una configuración del inyector inválida, con el código 2.

## Impact

- **Ficheros nuevos**:
  - el dominio en `src/Sandbox.Domain/`;
  - las operaciones, el estado in-memory, el catálogo semilla y el inyector en `src/Sandbox.Application/`;
  - `tests/Sandbox.Application.Tests/` y tests nuevos en `tests/Sandbox.Domain.Tests/` y `tests/Sandbox.Scenarios.Tests/`;
  - `docs/learn/11-dominios-aptos-para-agentes.md` y `docs/adr/0003-…`.
- **Ficheros modificados**:
  - `AgenticSandbox.slnx`, `Directory.Packages.props` y `Dockerfile`;
  - el `.csproj` de `Sandbox.Application`;
  - `src/Sandbox.Runner/RunnerHost.cs`;
  - `docs/learn/README.md`, `docs/learn/glosario.md` y `docs/plan.md`.
- **Se borra** `tests/Sandbox.Domain.Tests/DomainAssemblyWiringTests.cs`, el test de cableado temporal.
- **Dependencias nuevas** (versiones consultadas en la API de NuGet el 2026-11-01; se reverifican en el apply):
  - en `Sandbox.Application`: `Microsoft.Extensions.Options` y, si no llega como transitiva, `Microsoft.Extensions.Logging.Abstractions` (10.0.14). No son librerías de IA, así que `solution-layout` lo permite y `LayerRulesTests` lo sigue comprobando;
  - solo en tests: `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 (MIT, sin dependencias en `net10.0`).
- **Comportamiento visible**: ninguno para quien ejecuta el Runner sin la sección `FaultInjection`. Una sección inválida pasa a dar el código 2. El check `image` de la CI no cambia: sin clave, sigue esperando el 2.
- **Conceptos nuevos y dónde se documentan**:
  - documento 11 (nuevo): máquinas de estado explícitas, errores deterministas con códigos estables, el patrón *Result* frente a excepciones, idempotencia y la respuesta perdida, inyección de fallos (*fault injection*) reproducible, `TimeProvider` y relojes falsos, y determinismo de `Random` con semilla;
  - ADR 0003: por qué el dominio devuelve resultados en lugar de lanzar excepciones, una decisión que afecta a las fases 4 y 5;
  - glosario: los términos nuevos;
  - backlog de `docs/learn/README.md`: "Idempotencia, sagas y compensación" queda en parte hecho, con las sagas pendientes.
