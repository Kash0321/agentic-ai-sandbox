# Tasks

Rama de trabajo: `feat/add-order-domain`. Referencias: `design.md` (decisiones D1 a D10 y experimentos del explore) y `specs/`.

Todos los tests llevan nombres `Metodo_Condicion_Resultado` y siguen Arrange/Act/Assert. Ninguno necesita red, clave ni esperas reales.

## 1. Dependencias

- [x] 1.1 Reverificar en la API de NuGet la última versión estable y la licencia (`.nuspec`) de:
  - `Microsoft.Extensions.Options`;
  - `Microsoft.Extensions.Logging.Abstractions`;
  - `Microsoft.Extensions.TimeProvider.Testing`.

  Anotar aquí el resultado y la fecha. Comprobar en el `.nuspec` de `Microsoft.Extensions.Options` si trae `DependencyInjection.Abstractions` y `Logging.Abstractions` como transitivas, y referenciar solo lo que falte. Si alguna licencia no es MIT, Apache-2.0 u otra permisiva, parar y consultar.

  **Resultado (API de NuGet, `v3-flatcontainer`, 2026-11-01):**
  - `Microsoft.Extensions.Options` 10.0.14, MIT. En `net10.0` depende de `DependencyInjection.Abstractions` y `Primitives` 10.0.14, pero **no** de `Logging.Abstractions`.
  - `Microsoft.Extensions.Logging.Abstractions` 10.0.14, MIT. Se referencia de forma explícita.
  - `Microsoft.Extensions.TimeProvider.Testing` 10.11.0, MIT, sin dependencias en `net10.0` y con `requireLicenseAcceptance` a `true`.
- [x] 1.2 Añadir las versiones a `Directory.Packages.props` y las referencias al `.csproj` de `Sandbox.Application`. Comprobar si `ValidateOnStart` está disponible en `Microsoft.Extensions.Options` y anotarlo en `design.md` (suposición de D9). Verificar que `dotnet build` termina sin advertencias (incluida la auditoría de NuGet) y que `LayerRulesTests` sigue en verde.

## 2. Dominio (`Sandbox.Domain`)

- [x] 2.1 Crear `Result<T>`, `DomainError`, `ErrorKind` y `ErrorCodes` (D2). Tests en `Sandbox.Domain.Tests`:
  - un éxito expone el valor y no el error;
  - un fallo expone el error y no el valor;
  - los códigos de `ErrorCodes` son únicos y están en mayúsculas con guiones bajos.
- [x] 2.2 Crear `StockItem` con `Available`, `Reserve`, `Release` y `Consume` (D3). Tests de los escenarios "Reserva correcta", "Cancelar libera el stock" y "Enviar consume el stock" de la spec `order-domain`, más `INSUFFICIENT_STOCK` con un mensaje que nombra el producto y el disponible.
- [x] 2.3 Crear `Customer`, `Order`, `OrderLine`, `OrderStatus` y `OrderPolicy`, con `Order.Create` y las transiciones `Ship` y `Cancel` (D3). Tests, uno por escenario de la spec `order-domain`:
  - estado inicial;
  - las cinco transiciones, incluida "Cancelar un pedido ya cancelado";
  - `EMPTY_ORDER`, `INVALID_QUANTITY` (0, -1 y 101), los límites 1 y 100, y `DUPLICATE_ORDER_LINE`;
  - `CUSTOMER_BLOCKED`;
  - importe 1.000,01 frente a 1.000,00 exacto, y el precio fijado al crear;
  - que ninguna regla lanza excepción.
- [x] 2.4 Borrar `DomainAssemblyWiringTests` y verificar que `Sandbox.Domain.Tests` ejecuta los tests nuevos con `dotnet test`.
- [x] 2.5 Escribir `docs/adr/0003-errores-de-dominio-como-resultados.md`: contexto, decisión, alternativas y consecuencias para las fases 4 y 5 (D2).
- [x] 2.6 Documentación: crear `docs/learn/11-dominios-aptos-para-agentes.md`, con la estructura de `docs/learn/README.md`. En esta tarea, las secciones de:
  - máquinas de estado explícitas, con un diagrama Mermaid de la del pedido;
  - errores deterministas con códigos estables y su tipo;
  - el patrón *Result* frente a excepciones, enlazando el ADR 0003.

  Añadir los términos al glosario. Revisar que los diagramas ASCII solo usan los caracteres permitidos (`AGENTS.md`, sección 7).

## 3. Operaciones (`Sandbox.Application`)

- [x] 3.1 Crear `tests/Sandbox.Application.Tests` (D10) y añadirlo a `AgenticSandbox.slnx`, con un primer test real del grupo 3. Añadir su `COPY` del `.csproj` a la etapa `restore` del `Dockerfile`. Verificar que `dotnet test` ejecuta los cuatro proyectos de tests.
- [x] 3.2 Crear `SalesState` con su `lock` y `SeedCatalog` con los datos de D5. Tests:
  - "Catálogo reproducible": dos estados nuevos son iguales, con un cliente bloqueado y un producto sin stock;
  - un estado construido a medida en el test sustituye al catálogo.
- [x] 3.3 Crear `ISalesQueries`, sus queries, las vistas y su implementación (D4). Tests:
  - "Consultar el stock" (solo el disponible, sin existencias ni reservas);
  - `ORDER_NOT_FOUND`, `CUSTOMER_NOT_FOUND` y `PRODUCT_NOT_FOUND`;
  - "Las queries no cambian el estado", incluido el siguiente número de pedido.
- [x] 3.4 Crear `ISalesCommands` con `PlaceOrder` y la numeración `ORD-0001`, usando `TimeProvider` (D4, D5). Tests:
  - "Pedido creado", con la fecha fijada con `FakeTimeProvider`;
  - `CUSTOMER_NOT_FOUND` y `PRODUCT_NOT_FOUND`;
  - los dos escenarios de "Orden de evaluación de PlaceOrder";
  - stock insuficiente sin reservas parciales;
  - "Numeración tras un fallo".
- [x] 3.5 Añadir `CancelOrder` y `ShipOrder`, con la liberación y el consumo del stock dentro de la misma sección crítica. Tests de los escenarios de "Commands CancelOrder y ShipOrder" y de las transiciones inválidas a través de la aplicación (el stock no cambia).
- [x] 3.6 Añadir la idempotencia en los tres commands (D6). Tests, uno por escenario de la spec:
  - reintento de `PlaceOrder` y de `CancelOrder`, y reintento después de un cambio de estado (devuelve la vista guardada);
  - otra clave sobre un pedido ya cancelado;
  - `IDEMPOTENCY_KEY_CONFLICT` con otros argumentos, con las mismas líneas en otro orden y en otro command;
  - "Reintento tras stock insuficiente";
  - `INVALID_IDEMPOTENCY_KEY` (vacía, solo espacios, con un carácter de control y de 65 caracteres), y una de 64 aceptada.
- [x] 3.7 Tests de "Atomicidad frente a llamadas concurrentes": dos `PlaceOrder` lanzados a la vez compiten por 5 unidades; repetirlo en bucle (por ejemplo, 100 veces) para dar oportunidad a la carrera. Y de "Cancelación antes de ejecutar": con un token ya cancelado, `OperationCanceledException` y ningún pedido.
- [x] 3.8 Documentación: en el documento 11, las secciones de idempotencia (clave, huella, solo éxitos, el caso de la respuesta perdida, con un diagrama de secuencia Mermaid), CQRS simplificado y atomicidad del estado in-memory. Ampliar el glosario.

## 4. Inyector de fallos (`Sandbox.Application`)

- [x] 4.1 Crear `FaultInjectionOptions`, las reglas, sus enumerados y el validador (D7). Tests, uno por motivo de rechazo de la spec:
  - operación desconocida;
  - `FirstCalls` sin número válido;
  - probabilidad fuera de 0 a 1;
  - latencia negativa o mayor de 30 s;
  - una regla que ni falla ni añade latencia.

  Cada mensaje nombra la ruta del ajuste (`FaultInjection:Rules:<n>:<Ajuste>`). Una configuración vacía y una válida pasan.
- [x] 4.2 Crear el inyector (evaluación del plan bajo `lock`, un `Random` por regla con semilla `Seed + índice`) y los decoradores de commands y queries (D7). Tests:
  - "Sin reglas": resultados idénticos y sin espera;
  - regla sobre una operación y sobre `*`;
  - `Always`;
  - `FirstCalls` con N = 2;
  - "Misma semilla, mismo patrón", "Otras operaciones no alteran el patrón" y "Probabilidades extremas" (sin fijar valores concretos de la secuencia, D8);
  - `SERVICE_UNAVAILABLE` con tipo `Transient`.
- [x] 4.3 Añadir el momento `Before`/`After` y la combinación de reglas. Tests:
  - "PlaceOrder falla antes";
  - "Respuesta perdida y reintento idempotente" con `CancelOrder`;
  - "Latencia global y fallo concreto".
- [x] 4.4 Añadir la latencia con `Task.Delay(…, TimeProvider, ct)` fuera de cualquier `lock`. Tests con `FakeTimeProvider`, con el patrón "lanzar, comprobar que no ha terminado, `Advance` y `await`" y un `Timeout` de xUnit (D8):
  - "Latencia sin fallo";
  - "Cancelación durante la latencia".
- [x] 4.5 Añadir el registro con `LoggerMessage` y un `ILoggerProvider` mínimo de pruebas en `Sandbox.Application.Tests` (D10). Test de "Traza de un fallo inducido": operación, regla 0 y momento `After`, sin cliente ni líneas. **Hecho con un `ILogger<T>` de pruebas** (`Fakes/RecordingLogger.cs`), que basta porque el inyector recibe su registro por constructor. Observado: `LoggerMessage` formatea una colección como `[0, 1]`.
- [x] 4.6 Documentación: en el documento 11, la sección de inyección de fallos. Debe cubrir:
  - los disparadores y el momento;
  - por qué se decide el plan antes de ejecutar;
  - `TimeProvider` y `FakeTimeProvider`, con la trampa del `await` sin `Advance` observada en el explore;
  - `Random` con semilla: lo que garantiza y lo que no según Microsoft Learn, con la fecha, y el `GetHashCode` que cambia entre procesos.

  Distinguir lo comprobado de lo que es criterio de diseño. Ampliar el glosario.

## 5. Runner

- [x] 5.1 Crear `AddSandboxApplication` (D9) y llamarlo desde `RunnerHost.CreateBuilder`, enlazando la sección `FaultInjection`. Test en `Sandbox.Scenarios.Tests`: "Operaciones disponibles en el contenedor de servicios" (`GetCustomer` de `CUST-003` devuelve la vista de un cliente bloqueado).
- [x] 5.2 Comprobar con un test qué lanza `StartAsync` con dos opciones inválidas a la vez y anotarlo en `design.md` (suposición de D9). Si es `AggregateException`, traducirlo al código 2 en `RunAsync` cuando todas sus excepciones internas sean `OptionsValidationException`. Tests del flujo completo con `FakeChatClient`:
  - "Sin sección del inyector" (código 0);
  - "Configuración del inyector inválida" (código 2, sin llamar al modelo, el mensaje nombra el ajuste);
  - "Varias secciones inválidas a la vez" (código 2 y los dos ajustes nombrados).
- [x] 5.3 Comprobar que el *binder* lee enumerados como texto (`"FirstCalls"`) y `TimeSpan` (`"00:00:00.200"`) con un test que enlaza una configuración en memoria, y anotarlo en `design.md` (suposición del explore).
- [x] 5.4 Documentación: añadir al documento 10 la sección `FaultInjection` como ejemplo de opciones validadas al arrancar, y lo observado en 5.2 sobre varias opciones inválidas. Añadir al documento 11 cómo activar el inyector en el Runner, con un ejemplo de configuración por línea de comandos o variables de entorno.

## 6. Contenedor e integración

- [x] 6.1 Verificar en contenedor:
  - `docker build --target test .` en verde, con los tests de los cuatro proyectos y la restauración de `TimeProvider.Testing` sin intervención (`requireLicenseAcceptance`);
  - la imagen final sigue terminando con código 2 sin clave, con las mismas opciones que el job `image`;
  - con un canario en `secrets/` y otro en `.env`, ninguno aparece en la imagen de test ni en la final.

  Borrar los canarios. Al terminar, salir de Docker Desktop y ejecutar `wsl --shutdown`.
- [x] 6.2 Actualizar `docs/learn/README.md`:
  - índice con el documento 11;
  - fila de la fase 3;
  - backlog: "Idempotencia, sagas y compensación" en parte hecho, con las sagas multipaso pendientes para un dominio ampliado;
  - temas nuevos que hayan surgido.

  Actualizar `docs/plan.md`: tabla del change de la fase 3 y corrección de la fila de la fase 2, que sigue diciendo "pendiente de fusionar". Revisar si el `README.md` raíz necesita mencionar el dominio o el inyector.
- [x] 6.3 Validar y cerrar:
  - `openspec validate --all --strict` sin errores;
  - `dotnet build` sin advertencias y `dotnet test` en verde, sin red ni clave;
  - revisión de que ningún fichero versionado contiene secretos, rutas locales ni datos reales (el catálogo es ficticio).

## Workflow follow-up

- `/opsx:verify` sin problemas críticos y `/opsx:archive` en esta misma rama, antes de abrir el PR. Commits, push y PR solo cuando el usuario lo pida. En el PR, los checks `test`, `image` y `openspec` deben estar en verde.
- Fase 4: decidir si hacen falta `ListProducts` o `ListOrders` (preguntas abiertas de `design.md`) y traducir `ErrorKind` a `Retryable` en `ToolError`.
- Fase 5: el `FunctionInvokingChatClient` ya limita a 40 iteraciones y 3 errores seguidos por defecto (explore). El guardrail de iteraciones debe decidir si lo ajusta o lo sustituye.
- Futuro: dominio ampliado con sagas multipaso (petición del responsable en el explore).
