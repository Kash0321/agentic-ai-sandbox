# Tasks

Rama de trabajo: `feat/add-sales-agent`. Referencias: `design.md` (decisiones D1 a D10 y experimentos del explore) y `specs/`.

Todos los tests llevan nombres `Metodo_Condicion_Resultado`, siguen Arrange/Act/Assert y no necesitan red ni clave. No hay paquetes nuevos: se usan `Microsoft.Extensions.AI` y `.AI.OpenAI` 10.11.0, ya en `Directory.Packages.props`. Si la implementación necesitara alguno, se verifica en la API de NuGet y se anota la fecha (`AGENTS.md`, sección 11).

## 1. Query ListProducts (`Sandbox.Application`)

- [x] 1.1 Añadir `ListProductsQuery`, `ISalesQueries.ListProductsAsync` y su implementación, ordenada por SKU con comparación ordinal (D1). Tests en `Sandbox.Application.Tests`:
  - "Catálogo semilla" (orden y disponibles);
  - "Refleja las reservas";
  - que no cambia el estado (el siguiente pedido sigue siendo `ORD-0001`).
- [x] 1.2 Añadir `ListProducts` a `FaultOperations` y al decorador de queries. Tests:
  - el escenario "Regla sobre el listado de productos";
  - el validador acepta `ListProducts` como operación.

## 2. Contrato y borde de las tools (`Sandbox.Agents`)

- [x] 2.1 Comprobar con un test de aprendizaje las suposiciones de D3 y anotar el resultado en `design.md`:
  - que el `FunctionInvoker` recibe el `ArgumentException` de un argumento ausente;
  - que una `OperationCanceledException` de una tool se propaga fuera de `FunctionInvokingChatClient`.
- [x] 2.2 Crear `ToolResult<T>`, `ToolError`, la traducción desde `Result<T>` (`Retryable` solo para `Transient`) y los códigos propios `INTERNAL_ERROR` e `INVALID_ARGUMENTS` (D2). Tests:
  - éxito sin error;
  - fallo sin datos;
  - `Retryable` de cada `ErrorKind`.
- [x] 2.3 Crear las opciones de serialización de las tools: las de `AIJsonUtilities` con el codificador relajado (D5). Test: el resultado serializado lleva `"status":"Placed"`, ningún campo nulo y "Lámpara" sin escapar. Anotar en `design.md` si el esquema también sale sin escapar.

## 3. Tools (`Sandbox.Agents`)

- [x] 3.1 Crear el objeto de sesión y las seis tools en una clase dedicada (`SalesTools`), con nombres explícitos, descripciones en cada tool y parámetro, el `record` de línea con `[property: Description]` y el borde interior (D3, D4, D6). Tests de esquema, uno por tool:
  - nombre;
  - parámetros obligatorios exactos (sin `CancellationToken` ni cliente);
  - descripciones no vacías.
- [x] 3.2 Tests del caso feliz de cada tool, sobre el catálogo semilla:
  - `list_products` (cuatro productos);
  - `get_stock`;
  - `get_customer` (`CUST-003` bloqueado);
  - `get_order`;
  - `place_order` (pedido a nombre del cliente de sesión `CUST-002`);
  - `cancel_order`.
- [x] 3.3 Tests de cada código de error de la spec:
  - `get_stock`: `PRODUCT_NOT_FOUND`;
  - `get_customer`: `CUSTOMER_NOT_FOUND` con un cliente de sesión inexistente;
  - `get_order`: `ORDER_NOT_FOUND`;
  - `place_order`: los diez códigos de la operación de creación (un caso por código) y el reintento con la misma clave;
  - `cancel_order`: `ORDER_NOT_FOUND`, `INVALID_STATE_TRANSITION` (otra clave), `INVALID_IDEMPOTENCY_KEY` e `IDEMPOTENCY_KEY_CONFLICT`;
  - `SERVICE_UNAVAILABLE` reintentable con una regla del inyector.
- [x] 3.4 Test de "Descripción de la clave de idempotencia" y de que las descripciones y el prompt citan solo nombres de tools que existen.

## 4. Agente (`Sandbox.Agents`)

- [x] 4.1 Crear `ScriptedChatClient` en `Sandbox.Agents.Tests/Fakes`: un guion de pasos (llamadas a tools con argumentos, o texto, con sus tokens) que guarda los mensajes recibidos. Enlazarlo en `Sandbox.Scenarios.Tests`. Test del propio doble: dos pasos devuelven lo programado y registran lo recibido.
- [x] 4.2 Crear el system prompt como recurso incrustado. Test de "Instrucciones enviadas al modelo":
  - es el primer mensaje;
  - menciona la clave de idempotencia;
  - no contiene `1000`, `1.000` ni el rango `1` a `100`.
- [x] 4.3 Crear el agente: pipeline por ejecución con el contador, `UseFunctionInvocation` y el `FunctionInvoker` como borde exterior, que registra y produce `INVALID_ARGUMENTS`. También el resultado de la ejecución (D7). Tests:
  - "Pedido en tres llamadas al modelo";
  - "Varias tools en una misma respuesta";
  - "Textos intermedios" (respuesta final, registro y resumen);
  - "Tokens acumulados";
  - "Falta un parámetro obligatorio" (`INVALID_ARGUMENTS` y la tool no se ejecuta);
  - "Fallo inesperado de la aplicación" (`INTERNAL_ERROR` y la excepción en el registro);
  - "Ejecución cancelada".
- [x] 4.4 Registro de cada invocación (nombre, duración y resultado) y aviso de razonamiento con tools. Tests:
  - "Invocación fallida" (sin producto, cantidad ni clave en el registro);
  - "Razonamiento activado";
  - "Razonamiento desactivado".

## 5. Runner

- [x] 5.1 Crear las opciones `Agent` (`Scenario`, `Prompt`, `CustomerId`) con los *switch mappings* `--scenario`, `--prompt` y `--customer`, su validador y `ValidateOnStart` (D8). Comprobar la suposición de los *switch mappings* y anotarla en `design.md`. Tests:
  - "Escenario desconocido";
  - "Escenario y texto a la vez";
  - "Texto vacío";

  los tres con código 2, sin llamar al modelo y nombrando la opción.
- [x] 5.2 Crear el catálogo de escenarios (`happy-order`, `lost-response`) y la suma de sus reglas del inyector con `PostConfigure`, y resolver el cliente de sesión. Tests:
  - las reglas de `lost-response` se suman a una regla configurada sin pisarla;
  - el orden de preferencia del cliente de sesión.
- [x] 5.3 Crear el `AgentRunner` y el despacho en `RunnerHost`: saludo sin argumentos; agente con escenario o texto; respuesta final y resumen por la salida estándar. Tests de extremo a extremo con el host real y `ScriptedChatClient`:
  - "Escenario feliz con cliente falso" (código 0, salida, `ORD-0001`, 18 disponibles);
  - "Respuesta perdida" (un solo pedido);
  - "Texto libre con otro cliente";
  - "Error del proveedor durante el agente" (código 3);
  - el saludo sin argumentos sigue igual.

## 6. Verificación real y documentación

- [x] 6.1 Ejecuciones reales con la clave de User Secrets (leída por el proceso, nunca impresa): `dotnet run --project src/Sandbox.Runner -- --scenario happy-order` y `-- --scenario lost-response`, de 2 a 4 ejecuciones en total. Anotar en `design.md` las llamadas, los tokens y si se reutilizó la clave.
- [x] 6.2 Verificar en contenedor:
  - `docker build --target test .` en verde;
  - la imagen final sin clave sigue terminando con código 2 (como el job `image`);
  - `docker compose run --rm runner --scenario happy-order` con la clave como secreto por fichero termina con código 0;
  - con un canario en `secrets/` y otro en `.env`, ninguno aparece en las imágenes.

  Borrar los canarios. Al terminar, salir de Docker Desktop y ejecutar `wsl --shutdown`.
- [x] 6.3 Escribir `docs/adr/0004-identidad-del-cliente-fuera-del-modelo.md` (D6).
- [x] 6.4 Escribir `docs/learn/12-diseno-de-tools-para-llms.md` con la estructura de `docs/learn/README.md`, con lo observado en el spike y en 6.1, distinguiendo lo comprobado del criterio de diseño. Revisar que los diagramas ASCII solo usan los caracteres permitidos.
- [x] 6.5 Actualizar con las trazas reales:
  - los documentos 02 y 03: el bucle y el protocolo, con las llamadas, los tokens, las llamadas en paralelo y los textos intermedios;
  - el documento 04: el razonamiento con tools.

  Ampliar el glosario.
- [x] 6.6 Actualizar:
  - `docs/learn/README.md`: índice, fila de la fase 4 y backlog ("Diseño de tools para LLMs" hecho, temas nuevos);
  - `docs/plan.md`: tabla del change de la fase 4;
  - `README.md` raíz: uso de `--scenario`, `--prompt` y `--customer`, y estado.
- [x] 6.7 Validar y cerrar:
  - `openspec validate --all --strict` sin errores;
  - `dotnet build` sin advertencias y `dotnet test` en verde, sin red ni clave;
  - revisión de que ningún fichero versionado contiene secretos, rutas locales ni datos reales.

## Workflow follow-up

- `/opsx:verify` sin problemas críticos y `/opsx:archive` en esta misma rama, antes de abrir el PR. Commits, push y PR solo cuando el usuario lo pida. Los checks `test`, `image` y `openspec` deben estar en verde.
- Fase 5:
  - límites propios de iteraciones y de tokens (hoy 40 y 3 por defecto);
  - allow-list formal;
  - confirmación de `cancel_order`;
  - `FORBIDDEN` en `get_order` para pedidos de otros clientes;
  - auditoría y OpenTelemetry.
- Fase 6: medir con más ejecuciones si el modelo reutiliza la clave, y ampliar el catálogo de escenarios.
- Pendiente de la fase 2: registro de uso en *streaming*, si se llega a usar.
