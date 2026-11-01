# Design

## Context

Motivación y alcance en `proposal.md`. Requisitos en `specs/order-domain`, `specs/order-operations` y `specs/fault-injection`, y en los deltas de `runner-host`, `solution-layout` y `container-build`.

Estado de partida observado el 2026-11-01, en la rama `feat/add-order-domain` creada desde `main` (`0c35b76`):

- **Código**:
  - `Sandbox.Domain` y `Sandbox.Application` no tienen código; `Sandbox.Application` ya referencia a `Sandbox.Domain`;
  - el Runner compone el host en `RunnerHost.CreateBuilder`. Valida `DeepSeekOptions` con `ValidateOnStart`, y en `RunAsync` traduce `OptionsValidationException` al código 2. No captura `AggregateException`.
- **Tests**:
  - `Sandbox.Domain.Tests` solo tiene `DomainAssemblyWiringTests`, marcado como temporal "hasta la fase 3";
  - `LayerRulesTests` (en `Sandbox.Scenarios.Tests`) prohíbe librerías de IA en el dominio y la aplicación, y referencias hacia capas superiores.
- **Contenedor**: el `Dockerfile` copia un `.csproj` por proyecto en la etapa `restore`, a propósito: "si falta uno, la restauración de la solución falla de forma explícita". Un proyecto de tests nuevo exige una línea más.
- **Paquetes**: `Directory.Packages.props` tiene `Microsoft.Extensions.AI`, `.AI.OpenAI`, `.Hosting`, `.Configuration.KeyPerFile` y `xunit.v3`.

### Explore previo: experimentos y resultados

El explore se hizo el 2026-11-01. Las decisiones de alcance las tomó el responsable con preguntas interactivas. Los experimentos usaron una consola desechable en el scratchpad de la sesión (`spike-fase3`), fuera del repositorio, con el SDK 10.0.403 y el runtime 10.0.14.

**Decisiones del responsable:**

| Tema | Decisión |
|---|---|
| Alcance | Mínimo: `PlaceOrder`, `CancelOrder` y `ShipOrder`; `GetOrder`, `GetCustomer` y `GetStock`. Ampliar más adelante (sagas multipaso) |
| Cancelar un pedido ya cancelado | Error `INVALID_STATE_TRANSITION`, no éxito idempotente |
| Choque con el fallo `After` | Clave de idempotencia en los tres commands |
| Inyector | Contadores, probabilidad con semilla y latencia, parametrizables. Momento `Before` y `After` |
| Errores | `Result<T>` propio, sin excepciones de negocio |
| Reloj | `TimeProvider`; `FakeTimeProvider` en tests |
| Datos iniciales | Catálogo fijo en código; los tests construyen su propio estado |
| Tests de aplicación | Proyecto nuevo `Sandbox.Application.Tests` |
| Runner | Registra los servicios y valida el inyector al arrancar; el saludo no cambia |
| Pertenencia | Solo el dato (`CustomerId`); la comprobación llega en la fase 5 |
| Changes | Uno |

**Consultas a fuentes primarias (2026-11-01):**

| Qué | Resultado | Fuente |
|---|---|---|
| `Microsoft.Extensions.TimeProvider.Testing` | Última estable 10.11.0; licencia MIT (`<license type="expression">`); sin dependencias en `net10.0`; `requireLicenseAcceptance` a `true` | API de NuGet (`v3-flatcontainer`, índice y `.nuspec`) |
| `Microsoft.Extensions.Options`, `.DependencyInjection.Abstractions`, `.Options.ConfigurationExtensions` | Última estable 10.0.14 | API de NuGet |
| `Random` con semilla | "The implementation of the random number generator in the Random class isn't guaranteed to remain the same across major versions of .NET. As a result, you shouldn't assume that the same seed will result in the same pseudo-random sequence in different versions of .NET." | Microsoft Learn, *System.Random class* (notas complementarias) |
| `Random` y concurrencia | "Random objects are not thread safe. [...] calls to methods that return random numbers return 0." | Misma página, *Thread safety* |
| `AllowConcurrentInvocation` | Por defecto `false`: las *function calls* de una respuesta se procesan en serie. La página documenta la versión 10.10.0 | Microsoft Learn, API de `FunctionInvokingChatClient` |

**Experimentos:**

| Experimento | Observado |
|---|---|
| `Task.Delay(200 ms, fake)` con `FakeTimeProvider` | No completa tras `Advance(199 ms)`; completa tras 1 ms más, en 13 ms reales. El reloj marca exactamente +200 ms |
| `await` de ese delay sin `Advance` | **Se queda colgado**: tras 500 ms reales sigue sin completar. Es la trampa principal de los tests con latencia |
| `AutoAdvanceAmount = 250 ms` | Una sola lectura del reloj basta para completar un delay de 200 ms |
| `Task.Delay(TimeSpan.Zero, fake)` | Completa al instante, sin avanzar el reloj |
| Cancelar el token durante el delay | `TaskCanceledException`, que deriva de `OperationCanceledException` |
| `new Random(42)`, seis `NextDouble()` | Misma secuencia en el mismo proceso y en una segunda ejecución (`0.668, 0.141, 0.126, 0.523, 0.168, 0.263`); con 43, otra distinta |
| `"PlaceOrder".GetHashCode()` | `2062505648` en una ejecución y `667311391` en otra: **cambia entre procesos** |
| `FunctionInvokingChatClient` en `Microsoft.Extensions.AI` 10.11.0 (leído del ensamblado) | `AllowConcurrentInvocation=false`, `MaximumIterationsPerRequest=40`, `MaximumConsecutiveErrorsPerRequest=3`, `IncludeDetailedErrors=false` |

**Suposiciones sin verificar** (se comprueban en las tareas):

- Que `ValidateOnStart` esté en `Microsoft.Extensions.Options` (y no solo en `Hosting`), para poder llamarlo desde `Sandbox.Application`. **Comprobado en el apply (tarea 1.2)**: el ensamblado de `Microsoft.Extensions.Options` 10.0.14 contiene `OptionsBuilderExtensions.ValidateOnStart` y la interfaz `IStartupValidator`, y `Sandbox.Application` compila llamándolo.
- Que, con dos tipos de opciones inválidos a la vez, `StartAsync` lance un `AggregateException` en lugar de un `OptionsValidationException`. Con uno solo se observó lo segundo en la fase 2. **Confirmado en el apply (tarea 5.2)**: sin clave y con una regla inválida, `StartAsync` lanza un `AggregateException` con dos `OptionsValidationException` dentro (test `StartAsync_WithTwoInvalidOptionTypes_ThrowsAggregateOfOptionsValidationExceptions`). `RunAsync` lo traduce ahora al código 2 y muestra todos los fallos.
- Que el *binder* de configuración convierta a enumerados textos como `"FirstCalls"` y a `TimeSpan` textos como `"00:00:00.200"`. **Confirmado en el apply (tarea 5.3)**: el test `CreateBuilder_WithFaultInjectionSection_BindsEnumsAsTextAndLatencyAsTimeSpan` enlaza una configuración en memoria con enumerados como texto, `"00:00:00.200"` y `"0.3"`, y rellena la lista `Rules` aunque la propiedad sea de solo lectura (`IList<FaultRule> { get; }`).
- Que `Microsoft.Extensions.Logging.Abstractions` llegue como dependencia transitiva de `Microsoft.Extensions.Options`, o haya que referenciarlo. **Comprobado en el apply (tarea 1.1)**: no llega. En `net10.0`, `Options` solo depende de `DependencyInjection.Abstractions` y `Primitives`, así que `Logging.Abstractions` se referencia de forma explícita.

## Goals / Non-Goals

**Goals:**

- Que cada operación de la fase 4 se pueda exponer como tool con un mapeo directo: argumentos de entrada, `Result<T>` de salida y un código estable que traducir.
- Que todo el comportamiento, incluidos los fallos inducidos y la latencia, se pruebe sin esperas reales y con resultados repetibles.
- Que el dominio siga sin conocer la aplicación ni el LLM, y la aplicación siga sin conocer el LLM.

**Non-Goals:**

- Un *framework* de CQRS, de *mediator* o de eventos de dominio.
- Tipos de valor para dinero o identificadores (`Money`, `OrderId`). Un `decimal` en euros y cadenas con formato bastan para este alcance.
- Concurrencia de alto rendimiento: un único `lock` es suficiente.

## Decisions

### D1. Reparto entre capas

| Pieza | Proyecto | Por qué |
|---|---|---|
| `Order`, `OrderLine`, `OrderStatus`, `Customer`, `CustomerStatus`, `StockItem` | `Sandbox.Domain` | Reglas de negocio (`AGENTS.md`, principio 2) |
| `Result<T>`, `DomainError`, `ErrorKind` y el catálogo de códigos (`ErrorCodes`) | `Sandbox.Domain` | Lo usan el dominio y la aplicación; la fase 4 lo traduce a `ToolError` |
| Límites de negocio (`OrderPolicy`: cantidad 1–100, importe máximo 1.000,00 EUR) | `Sandbox.Domain` | Son reglas, no configuración (ver Riesgos) |
| Commands, queries, vistas, estado in-memory, idempotencia y catálogo semilla | `Sandbox.Application` | Orquestan el dominio y guardan el estado |
| Inyector de fallos y sus opciones | `Sandbox.Application` | Simula fallos en el borde del servicio, no en las reglas |
| Registro en DI (`AddSandboxApplication`) | `Sandbox.Application` | La aplicación sabe cómo componerse; el Runner solo la invoca |
| Enlace de la sección `FaultInjection` a la configuración | `Sandbox.Runner` | Así `Sandbox.Application` no depende de `Microsoft.Extensions.Configuration` |

`Sandbox.Application` gana dependencias de abstracciones de `Microsoft.Extensions` (opciones, DI y registro), ninguna de IA. La separación entre dominio e invocación del LLM no cambia: `LayerRulesTests` lo sigue comprobando sin modificarse.

### D2. Errores como resultado (ADR 0003)

```
Result<T>      = Success(T value) | Failure(DomainError error)
DomainError    = (string Code, string Message, ErrorKind Kind)
ErrorKind      = Validation | NotFound | BusinessRule | Conflict | Transient
```

- `Result<T>` es un `record` propio, mínimo y sin dependencias. Lleva `IsSuccess`, `Value` y `Error`, y fábricas `Success`/`Failure`. No tiene encadenamiento monádico (`Bind`/`Map`) mientras nadie lo necesite.
- Los códigos son constantes en una sola clase (`ErrorCodes`), para que los tests y la fase 4 los referencien sin cadenas sueltas.
- `ErrorKind` existe para que la fase 4 derive `Retryable` (solo `Transient`) sin que el dominio conozca `ToolError`. Así se mantiene la separación entre dominio e invocación del LLM.
- Los mensajes van en español, son breves y accionables, y nunca llevan trazas ni nombres internos. Solo `INSUFFICIENT_STOCK` y `PRODUCT_NOT_FOUND` incluyen datos (el producto y el disponible), porque el agente los necesita para corregir la petición.

**Trasciende este change**: las fases 4 (tools) y 5 (guardrails) se apoyan en este contrato. Por eso se registra como `docs/adr/0003-errores-de-dominio-como-resultados.md`.

Alternativas:
- Excepciones tipadas: el flujo de error queda oculto, y es fácil que una se escape hasta el LLM, cosa que prohíbe `AGENTS.md` 4.4.
- `ErrorOr` o `FluentResults`: añaden una dependencia al dominio para unas 40 líneas de código.

### D3. Modelo de dominio

```
Customer (Id, Name, Status: Active | Blocked)

StockItem (Sku, Name, UnitPrice, OnHand, Reserved)
   Available = OnHand - Reserved
   CanReserve(q) / Reserve(q) / Release(q) / Consume(q)

Order (Id, CustomerId, Lines, Total, Currency = EUR, Status, PlacedAt)
   Create(id, customer, pricedLines, now) -> Result<Order>
   Ship()   : Placed -> Shipped      | otro -> INVALID_STATE_TRANSITION
   Cancel() : Placed -> Cancelled    | otro -> INVALID_STATE_TRANSITION
```

- **Ajustado en el apply (tarea 2.3)**: la spec `order-domain` pone en el dominio la reserva, la liberación y el consumo del stock, así que `Order` los aplica él mismo, no la aplicación. `Order.Create(id, customer, lines, stock, placedAt)` recibe el stock como diccionario por SKU y evalúa, en este orden, líneas, cliente bloqueado, existencia de cada producto, importe y stock. Después reserva todas las líneas, todo o nada. `Ship(stock)` y `Cancel(stock)` aplican la transición y su efecto sobre el stock en el mismo paso.
- La aplicación solo comprueba lo que exige consultar su estado: la existencia del cliente. Para respetar el orden de la spec (las líneas antes que el cliente), llama antes a `OrderPolicy.ValidateLines`. `Order.Create` las vuelve a validar, para que el dominio no dependa de que se haya hecho.
- Las líneas pedidas son un `record` del dominio (`OrderLineRequest`), que el command de la aplicación reutiliza.
- Las transiciones devuelven `Result` y solo cambian el estado si tienen éxito. La aplicación las llama dentro de la misma sección crítica (D5).
- El estado mutable de las entidades queda encapsulado (`private set`). Las vistas que salen de la aplicación son `record` inmutables (`AGENTS.md`, principio 4).
- El precio unitario se copia en la línea al crear el pedido.

### D4. Operaciones (CQRS simplificado)

```
ISalesCommands                                 ISalesQueries
  PlaceOrderAsync(PlaceOrderCommand, ct)         GetOrderAsync(GetOrderQuery, ct)
  CancelOrderAsync(CancelOrderCommand, ct)       GetCustomerAsync(GetCustomerQuery, ct)
  ShipOrderAsync(ShipOrderCommand, ct)           GetStockAsync(GetStockQuery, ct)
        -> Task<Result<OrderView>>                    -> Task<Result<OrderView | CustomerView | StockView>>
```

- Dos interfaces, una de escritura y otra de lectura, para que la fase 5 aplique guardrails distintos a cada una y el inyector las decore por separado.
- Commands y queries son `record` posicionales. Las líneas de `PlaceOrder` son `IReadOnlyList<OrderLineRequest(Sku, Quantity)>`.
- Los métodos son `async` aunque el estado esté en memoria: la latencia inducida es asíncrona, y la fase 4 los invocará desde tools asíncronas. Comprueban la cancelación al entrar.
- No hay *mediator*: la fase 4 inyecta las interfaces en las clases de tools.
- **Orden de evaluación de `PlaceOrder`** (spec `order-operations`): clave, líneas, existencia del cliente, cliente bloqueado, existencia de productos, importe y stock. Las tres primeras validaciones de líneas y el importe los decide `Order.Create`; la aplicación lo llama en el punto adecuado de la secuencia.

### D5. Estado in-memory y atomicidad

- Una sola clase de estado (`SalesState`) contiene clientes, stock, pedidos, el contador de pedidos y el almacén de idempotencia, protegidos por **un único `lock`**.
- Cada command hace todo su trabajo dentro de una sola sección crítica: idempotencia, validación, reserva de todas las líneas y registro del resultado. Así se garantiza que se reserva todo o nada y que la numeración no se rompe con dos llamadas concurrentes.
- **Nada asíncrono dentro del `lock`**, que en C# no lo admite. La latencia inducida ocurre fuera, en el decorador (D7).
- El catálogo semilla (`SeedCatalog`) crea un estado nuevo con estos datos ficticios:

  | Clientes | Estado |
  |---|---|
  | `CUST-001` Ana Demo | Activo |
  | `CUST-002` Bruno Demo | Activo |
  | `CUST-003` Carla Demo | Bloqueado |

  | Producto | Nombre | Precio | Existencias |
  |---|---|---|---|
  | `SKU-MUG-RED` | Taza roja | 12,50 | 20 |
  | `SKU-TSHIRT-M` | Camiseta talla M | 19,90 | 5 |
  | `SKU-LAMP-DESK` | Lámpara de escritorio | 89,00 | 0 |
  | `SKU-CHAIR-ERGO` | Silla ergonómica | 249,00 | 10 |

  Con la silla, 4 unidades (996,00) caben en el límite y 5 (1.245,00) lo superan. Esto da escenarios de `ORDER_LIMIT_EXCEEDED` para la fase 6 sin tocar el catálogo.
- Los tests crean el estado con su propia lista de clientes y productos, por ejemplo un producto a 250,00 para probar el importe exacto de 1.000,00.

Alternativa: un `ConcurrentDictionary` por entidad. No garantiza la atomicidad entre stock y pedidos, y obligaría a compensar a mano.

### D6. Idempotencia

```
command(key, args)
   |
   +-- clave inválida -----------------------------------> INVALID_IDEMPOTENCY_KEY
   +-- clave registrada:
   |      misma operación y huella -----------------------> resultado guardado (sin efectos)
   |      otra operación u otra huella -------------------> IDEMPOTENCY_KEY_CONFLICT
   +-- clave nueva --> ejecutar
          éxito --> guardar (operación, huella, vista) ---> vista
          error ----------------------------------------> error (la clave no se guarda)
```

- **Huella**: la lista ordenada de los argumentos, comparada elemento a elemento con comparación ordinal. Para `PlaceOrder`, el cliente y, por cada línea en el orden recibido, el producto y la cantidad; para los otros, el pedido. No se usa `GetHashCode`. **Ajustado en el apply**: el borrador hablaba de una cadena canónica; una lista evita tener que escapar separadores y se comporta igual. Las mismas líneas en otro orden dan otra huella, y por tanto `IDEMPOTENCY_KEY_CONFLICT`. Se documenta como límite conocido.
- **Solo se guardan los éxitos** (spec): un error de negocio puede dejar de serlo cuando cambia el estado, y el agente debe poder reintentar con la misma clave.
- **Se guarda la vista del momento**: el reintento devuelve exactamente lo que devolvió la primera vez, aunque el pedido haya cambiado después.
- El almacén vive dentro de `SalesState`, bajo el mismo `lock`. Si fuera un decorador aparte, comprobar la clave y ejecutar no serían atómicos.
- Las claves no caducan; el estado vive lo que dura el proceso.

Alternativa: idempotencia como decorador genérico. Es más reutilizable, pero rompe la atomicidad o exige exponer el `lock`.

### D7. Inyector de fallos

```
llamada --> FaultInjecting{Commands|Queries}
              |
              1. plan = injector.Evaluate(operación)     (bajo su propio lock:
              |      para cada regla que cubre la         avanza contadores y Random
              |      operación, en orden declarado        de cada regla)
              |      -> latencia total, ¿falla?, momento
              2. await Task.Delay(latencia, TimeProvider, ct)  (fuera de cualquier lock)
              3. Before: devolver SERVICE_UNAVAILABLE
                 After:  await inner(...); descartar el resultado; devolver SERVICE_UNAVAILABLE
                 sin fallo: return await inner(...)
```

- **Decidir antes de ejecutar**: el plan se calcula en el paso 1. Así el patrón de fallos no depende de lo que tarde ni de lo que devuelva la operación.
- **Un `Random` por regla**, con semilla `Seed + índiceDeRegla`, creado al construir el inyector. Se accede con el `lock` del inyector porque `Random` no es *thread-safe* y, sin protección, devuelve 0 (con `p > 0`, eso significa fallar siempre). Nunca se deriva una semilla de `GetHashCode()`, que cambia entre procesos.
- **Configuración** (sección `FaultInjection`):

  ```json
  "FaultInjection": {
    "Seed": 42,
    "Rules": [
      { "Operation": "PlaceOrder", "Trigger": "FirstCalls", "Count": 1, "Timing": "After" },
      { "Operation": "GetStock", "Trigger": "Probability", "Probability": 0.3 },
      { "Operation": "*", "Latency": "00:00:00.200" }
    ]
  }
  ```

  `Trigger` vale `None` (por defecto: solo latencia), `Always`, `FirstCalls` o `Probability`. `Timing` vale `Before` (por defecto) o `After`. Las opciones son una clase con `set`, como `DeepSeekOptions`, porque el *binder* la rellena.
- **Validación**: un `IValidateOptions<FaultInjectionOptions>` propio con mensajes que nombran la ruta (`FaultInjection:Rules:1:Probability`) y el motivo. Se ejecuta con `ValidateOnStart`.
- **Registro**: `LoggerMessage` generado, con nivel `Warning` para los fallos y `Information` para la latencia. Lleva la operación, el índice de la regla, el momento y la latencia; nunca los argumentos.
- **Sin reglas**, el decorador sigue registrado, pero el plan vacío no espera ni falla (`Delay(Zero)` completa al instante, según el explore). Se acepta un decorador más a cambio de no tener dos caminos de registro.

Alternativas:
- Inyectar fallos dentro de los servicios: mezcla el fallo simulado con la lógica.
- Polly o `Microsoft.Extensions.Resilience` con su *chaos engineering*: es una dependencia grande para un laboratorio, y oculta justo lo que se quiere aprender.

### D8. Reloj y tests con latencia

- `TimeProvider` se inyecta en el estado (fecha de creación) y en el inyector (latencia). `AddSandboxApplication` registra `TimeProvider.System` con `TryAddSingleton`, y los tests lo sustituyen.
- Tests con latencia, siguiendo lo aprendido en el explore: **lanzar la llamada sin `await`, comprobar que no ha terminado, `Advance(latencia)` y después `await`**. Un `await` antes de avanzar el reloj deja el test colgado. Para evitar cuelgues silenciosos, esos tests llevan un `Timeout` de xUnit.
- Los tests de probabilidad comparan dos inyectores con la misma semilla, o un inyector con y sin llamadas intercaladas, y los extremos 0 y 1. **No fijan qué llamada concreta falla con la semilla 42**: la documentación no garantiza la secuencia entre versiones mayores de .NET.

### D9. Registro en el Runner y validación al arrancar

```csharp
builder.Services.AddSandboxApplication()            // devuelve OptionsBuilder<FaultInjectionOptions>
    .Bind(builder.Configuration.GetSection("FaultInjection"));
```

- `AddSandboxApplication` registra el estado con el catálogo semilla, los servicios, los decoradores, el validador y `ValidateOnStart`. El Runner solo enlaza la sección. Si `ValidateOnStart` no estuviera en `Microsoft.Extensions.Options` (suposición pendiente), la llamada iría al Runner.
- **Varias opciones inválidas a la vez**: si se confirma que `StartAsync` lanza un `AggregateException`, `RunAsync` lo trata como configuración inválida cuando todas sus excepciones internas sean `OptionsValidationException`, y muestra todos los fallos. Sin esto, faltar la clave y tener un inyector inválido daría el código 1 en vez del 2.
- El saludo no usa los servicios. Un test de `Sandbox.Scenarios.Tests` resuelve las interfaces desde el host para comprobar el registro (spec `runner-host`).

### D10. Proyecto de tests y contenedor

- `tests/Sandbox.Application.Tests`: ejecutable, xUnit v3, `<Using Include="Xunit" />`, referencias a `Sandbox.Application` y `Sandbox.Domain` y paquete `Microsoft.Extensions.TimeProvider.Testing`. Para los registros usa un `ILoggerProvider` mínimo propio. No enlaza el de `Sandbox.Agents.Tests`, que depende de su `LogEntry`, para no acoplar los proyectos de tests de capas distintas.
- `Dockerfile`: una línea `COPY` más en la etapa `restore` para el nuevo `.csproj`.
- `Sandbox.Domain.Tests` borra el test de cableado y pasa a probar el dominio.

## Risks / Trade-offs

- [Los límites de negocio (100 unidades, 1.000,00 EUR) son constantes y no configuración] → Son reglas del dominio, como dice `docs/learn/01` ("límites de negocio en el dominio"). Si la fase 6 necesita otros valores, los tests construyen estados a medida; hacerlos configurables sería otro change.
- [La huella depende del orden de las líneas] → Se documenta. Reordenar las líneas con la misma clave da conflicto, que es lo prudente.
- [Un fallo `After` oculta el resultado real, incluso un error de negocio] → Es lo que simula una respuesta perdida. El registro del inyector deja constancia de qué pasó de verdad.
- [El almacén de idempotencia crece sin límite] → El estado vive lo que dura el proceso del Runner, de una sola ejecución. Se anota para la fase 6 si los escenarios largos lo notan.
- [Un test de latencia mal escrito se cuelga] → Patrón de D8 y `Timeout` en esos tests.
- [`Random` con semilla cambia entre versiones mayores de .NET] → Los tests no fijan valores concretos (D8). Si un escenario necesita un fallo exacto, se usa `FirstCalls`.
- [Un único `lock` serializa todas las operaciones] → Es irrelevante a esta escala, y `FunctionInvokingChatClient` ya invoca en serie por defecto.
- [`requireLicenseAcceptance` en `TimeProvider.Testing`] → No impide `dotnet restore` desde la línea de comandos **(no verificado; se comprueba al restaurar, también en el contenedor)**.

## Migration Plan

No hay datos ni consumidores que migrar. Sin la sección `FaultInjection`, el Runner se comporta como antes. Para revertir basta con quitar el change: el Runner no depende funcionalmente de los servicios hasta la fase 4.

## Open Questions

- ¿Necesitará la fase 4 una query para listar productos (`ListProducts`), para que el agente descubra los SKU sin conocerlos? Se decide al diseñar las tools; no cambia este change.
- ¿Conviene un `ListOrders` por cliente? Depende de los escenarios de la fase 6 y de la autorización de la fase 5.
