# Tasks

Rama de trabajo: `feat/add-tool-approval`. Referencias: `design.md` (decisiones D1 a D9 y experimentos A1 a A5 del explore) y `specs/`.

Todos los tests llevan nombres `Metodo_Condicion_Resultado`, siguen Arrange/Act/Assert y no necesitan red, clave ni teclado. No hay paquetes nuevos: `ApprovalRequiredAIFunction` y los contenidos de aprobación están en `Microsoft.Extensions.AI` 10.11.0. Antes de dar un requisito por bueno, se mide su longitud: los MODIFIED no se validan hasta el archivado.

## 1. Contrato del aprobador y auditoría

- [x] 1.1 Añadir a `AuditLog` el evento 5005 `ApprovalDecided` (tool, `ApprovalOutcome`, aprobador y motivo) y el enum `ApprovalOutcome` (D6). Test: el evento sale en `Sandbox.Audit` con su identificador y sus campos, y sin argumentos.
- [x] 1.2 Crear en `Sandbox.Agents` el contrato `IToolApprover`, `ToolApprovalRequest` y `ToolApprovalDecision`. Añadir `CONFIRMATION_DENIED` a `ToolErrorCodes` y `SalesToolNames.RequiresConfirmation`, que contiene solo `cancel_order` (D3, D5). Tests: `RequiresConfirmation` es exactamente `cancel_order` y está en el catálogo.
- [x] 1.3 Crear `ScriptedToolApprover` en `Sandbox.Agents.Tests/Fakes`, enlazado en `Sandbox.Scenarios.Tests`: decide según un guion y guarda las peticiones. Test del propio doble: dos decisiones programadas se devuelven en orden y las peticiones quedan registradas.

## 2. Tests de aprendizaje de la aprobación

- [x] 2.1 Fijar en `Sandbox.Agents.Tests/Learning` el comportamiento de la librería (A1 a A4):
  - la petición vuelve como `ToolApprovalRequestContent` sin invocar;
  - lo aprobado pasa por el `FunctionInvoker`;
  - lo denegado no pasa y el modelo recibe el texto de rechazo con el motivo;
  - todo o nada con dos peticiones en una respuesta.
- [x] 2.2 Test de aprendizaje: la invocación aprobada recibe exactamente los argumentos de la petición de aprobación (D3).

## 3. Agente con confirmación (`Sandbox.Agents`)

- [x] 3.1 Envolver en `ApprovalRequiredAIFunction` las tools de `RequiresConfirmation` que la allow-list permite, y dejar el resto como hasta ahora (D4). Añadir a la descripción de `cancel_order` el aviso de confirmación (D8). Tests:
  - "Tool sin confirmación" (no se pregunta);
  - "Cancelación sin permiso" (sin preguntar, `BLOCKED_BY_POLICY`, pedido sin cambios);
  - el esquema de `cancel_order` sigue con los mismos parámetros.
- [x] 3.2 Convertir `RunAsync` en el bucle de reanudación, con el historial acumulado, la misma instancia de presupuesto y el mismo `RunState` en todas las vueltas, y la parada por guardrail en cualquier vuelta (D2). Tests:
  - "Cancelación pendiente de confirmar" (pedido en `Placed` cuando decide el aprobador);
  - "Cancelación aprobada" (argumentos recibidos, `Cancelled`, `Completed`);
  - los textos intermedios y el orden de las tools siguen bien con una pausa;
  - el límite de llamadas cubre el total de las vueltas.
- [x] 3.3 Decidir cada petición: el aprobador para las que requieren confirmación y la aprobación en código para el resto. Las denegaciones llevan el motivo, quedan registradas con `CONFIRMATION_DENIED` y cuentan como error seguido, y al alcanzar el límite se para sin reanudar (D3). Tests:
  - "Cancelación denegada";
  - "Consulta y cancelación juntas";
  - "Confirmaciones denegadas seguidas" (3 preguntas, `ToolErrorLimit`, sin una cuarta llamada al modelo).
- [x] 3.4 Emitir `ApprovalDecided` en cada decisión del aprobador, y no en las aprobaciones en código. Tests: "Denegación auditada" (sin `orderId` ni clave) y que el evento lleva el *scope* de ejecución.

## 4. Aprobador de consola (`Sandbox.Runner`)

- [x] 4.1 Crear `ConsoleToolApprover`, con la entrada, la salida de errores y la comprobación de consola interactiva inyectadas, y registrarlo en `RunnerHost` (D5). Tests con `StringReader`:
  - "Aprobación con s";
  - `SI` y `si` con espacios aprueban;
  - "Respuesta vacía" deniega;
  - otra palabra deniega;
  - el fin de la entrada deniega;
  - "Carácter de control en un argumento": `\u001B` en la salida y no el carácter;
  - los acentos se muestran sin escapar.
- [x] 4.2 Test de "Entrada redirigida": deniega con el motivo "sin consola interactiva" y sin leer la entrada (un lector que falla si se lee).

## 5. Escenario de cancelación (`Sandbox.Runner`)

- [x] 5.1 Añadir `SeedOrders` a `AgentScenario` y crearlos en `AgentRunner` con `SalesCommands`, sin el inyector, antes de llamar al agente (D7). Tests:
  - "Fallos inducidos intactos": un escenario de prueba con un pedido previo y la regla de `lost-response`, cuya regla sigue sin consumirse;
  - los pedidos previos no cuentan como llamadas al modelo ni como tools.
- [x] 5.2 Añadir el escenario `cancel-order` (ORD-0001 de `CUST-001` con dos tazas rojas). Tests de extremo a extremo con el Runner, el modelo falso y el aprobador con guion registrado en DI:
  - "Cancelación aprobada" (`Cancelled`, disponible 20);
  - "Cancelación denegada" (`Placed`, disponible 18, código 0, `cancel_order (CONFIRMATION_DENIED)` en la salida);
  - la petición del escenario cabe en la longitud máxima.

## 6. Documentación

- [x] 6.1 Actualizar el documento 05 con:
  - la confirmación humana: pausa y reanudación con su diagrama;
  - los experimentos A1 a A5;
  - el todo o nada y la relación con la allow-list;
  - qué ve el humano y por qué en JSON;
  - *fail closed* sin consola;
  - errores típicos.

  Verificación: cada afirmación sobre la librería cita la documentación (consultada el 2026-11-05) o el spike.
- [x] 6.2 Actualizar:
  - el documento 06: escenarios 4 y 5 cubiertos, y la fila "Elevación y aprobación" con lo implementado;
  - el documento 04: la fila de aprobación humana ya no "se emulará";
  - el glosario: aprobación fuera de banda, pausa y reanudación;
  - `docs/learn/README.md`: fila de la fase 5 y *backlog* de la confirmación humana marcado como hecho;
  - `docs/plan.md`: estado del change;
  - `README.md`: escenario `cancel-order` y la pregunta de la consola.

  Verificación: los comandos de ejemplo usan opciones que existen.

## 7. Comprobación de integración

- [x] 7.1 `dotnet build` sin advertencias y `dotnet test` con todos los tests en verde.
- [x] 7.2 `openspec validate --all --strict` sin errores y longitud de los requisitos MODIFIED medida.
- [x] 7.3 `docker build --target test` en verde y la imagen final sin clave terminando con el código 2. Comprobar si `docker compose run --rm runner --scenario cancel-order` abre una consola interactiva y anotarlo en el documento 07 o en el 05. Cerrar Docker Desktop y ejecutar `wsl --shutdown`.
- [x] 7.4 Ejecuciones reales contra DeepSeek:
  - `--scenario cancel-order` desde las herramientas del asistente, con la entrada no interactiva: se deniega y el pedido sigue en `Placed`;
  - la misma ejecución lanzada por el usuario en su terminal (`! dotnet run --project src/Sandbox.Runner -- --scenario cancel-order`), aprobando y denegando.

  Anotar en el documento 05 qué hace el modelo tras una denegación, las llamadas y los tokens.

## Workflow follow-up

- Ejecutar `/opsx:verify` y corregir lo crítico.
- Archivar con `/opsx:archive` en la misma rama, con las specs principales sincronizadas.
- Commit, push y PR solo cuando el usuario lo pida. El PR debe tener en verde los checks `test`, `image` y `openspec`.
