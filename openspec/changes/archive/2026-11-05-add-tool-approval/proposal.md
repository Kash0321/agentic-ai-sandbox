# Proposal

## Why

`cancel_order` es la única acción **irreversible** del agente: `Cancelled` es un estado final. `AGENTS.md` (sección 4.7) exige confirmación explícita para estas acciones, y los documentos 05 y 06 piden que vaya **ligada a la acción y a los argumentos exactos**, nunca un "¿continuar?" genérico. Hoy el modelo la ejecuta en cuanto la pide. El primer change de la fase 5 (`add-agent-guardrails`) puso autorización, límites, allow-list y auditoría, pero dejó fuera la confirmación humana.

Este change es el segundo de los tres de la **fase 5** de `docs/plan.md` (*Guardrails*). El explore (spike con `Microsoft.Extensions.AI` 10.11.0, recogido en `design.md`) decidió usar el mecanismo estándar de la librería, `ApprovalRequiredAIFunction`. Se encontraron cuatro hechos que condicionan el alcance:

- Ante una tool marcada, el bucle **no la invoca**: devuelve el control al llamador con un `ToolApprovalRequestContent` que envuelve la petición exacta, y se reanuda cuando el llamador envía la respuesta. Es el patrón de **pausa y reanudación** que usan las aplicaciones reales, donde la aprobación llega por otro canal.
- Lo aprobado **sí pasa por nuestro `FunctionInvoker`**, así que la allow-list, los errores seguidos y la auditoría siguen aplicándose. La denegación **no pasa**: el modelo recibe un texto de la librería, y hay que registrarla y auditarla en el paso de aprobación.
- **Todo o nada**: si una respuesta pide una tool marcada, todas las peticiones de esa respuesta pasan a requerir aprobación.
- Estos tipos **no son experimentales** en la 10.11.0: compilan sin avisos con los avisos tratados como errores.

## What Changes

- **Confirmación de acciones irreversibles** (`Sandbox.Agents`):
  - `cancel_order` queda marcada en código como tool que requiere confirmación. No es configurable.
  - Cuando está permitida, se ofrece envuelta en `ApprovalRequiredAIFunction`. Si la allow-list no la permite, no se pregunta a nadie: sigue recibiendo `BLOCKED_BY_POLICY`.
  - `SalesAgent` atiende las peticiones de aprobación y reanuda el bucle. Las tools que no requieren confirmación y llegan en la misma respuesta se aprueban en código, sin preguntar al humano.
  - Se pregunta a un **aprobador** inyectable con la tool y sus argumentos exactos. Lo que se aprueba es exactamente lo que se ejecuta.
  - Si se deniega, no se ejecuta nada. El modelo recibe un motivo accionable; el resultado registra la tool con `CONFIRMATION_DENIED`; la denegación cuenta como error seguido.
  - Cada decisión deja un evento de auditoría `ApprovalDecided`, con la tool, la decisión y quién decidió.
- **Aprobadores**:
  - de **consola** (Runner): muestra la tool y sus argumentos en JSON por la salida de errores y pregunta `[s/N]`. Solo `s`, `si` o `sí` aprueban. Sin consola interactiva (CI, stdin redirigido) **deniega** (*fail closed*);
  - **con guion** (tests): aprueba o deniega según lo programado y guarda lo que se le preguntó.
- **Escenarios con pedidos previos** (`Sandbox.Runner`):
  - un escenario puede sembrar pedidos al arrancar, con los commands de la aplicación sin el inyector de fallos;
  - escenario nuevo `cancel-order`: `CUST-001` ya tiene `ORD-0001` y pide cancelarlo.
- **Tests**:
  - tests de aprendizaje que fijan el comportamiento de la librería (A1 a A4, y que los argumentos aprobados son los ejecutados);
  - tests del agente con aprobación y denegación, todo o nada y allow-list;
  - tests del aprobador de consola, con entradas simuladas y sin consola interactiva;
  - escenario `cancel-order` de extremo a extremo.
- **Documentación**:
  - documento 05 (confirmación humana: pausa y reanudación, todo o nada, la denegación fuera del `FunctionInvoker`, qué ve el humano y por qué en JSON);
  - documento 06 (escenarios 4 y 5 cubiertos);
  - documento 04 (donde decía "se emulará con un guardrail propio");
  - glosario, índice y *backlog*, `docs/plan.md` y `README.md`.

### Fuera de alcance

- **Aprobación previa por opción** (`--approve`) o política automática: se descartó en el explore, porque el humano aprobaría sin ver la acción concreta.
- **Aprobación fuera de banda real** (web, Teams) y **persistencia** de la conversación entre ejecuciones: el Runner sigue siendo *one-shot*. El patrón queda preparado, pero solo con un aprobador que contesta en el momento.
- **Tiempo de espera** de la aprobación: Ctrl+C cancela.
- **Confirmación de `place_order`**: es reversible (se cancela).
- **OpenTelemetry y Aspire Dashboard**: tercer change de la fase 5.
- **Cambios en el `Dockerfile` o en la CI**: el check `image` no ejecuta el agente.

## Capabilities

### New Capabilities

- `tool-approval`: la confirmación humana de las acciones irreversibles:
  - qué tools la requieren;
  - cómo se pide (acción y argumentos exactos) y qué pasa al aprobar y al denegar;
  - el todo o nada y la relación con la allow-list;
  - los aprobadores de consola y con guion;
  - el evento de auditoría de cada decisión.

### Modified Capabilities

- `agent-guardrails`, "Límite de errores de tool seguidos": una confirmación denegada cuenta como error.
- `sales-agent`: requisitos nuevos para los pedidos previos de un escenario y para el escenario `cancel-order`. "Escenarios con nombre" no cambia: ya roza el límite de longitud.

## Impact

- **Ficheros nuevos**:
  - en `src/Sandbox.Agents/`: el contrato del aprobador y la petición de aprobación;
  - en `src/Sandbox.Runner/`: el aprobador de consola;
  - en los tests: el aprobador con guion, tests de aprendizaje, del agente, del aprobador de consola y del escenario.
- **Ficheros modificados**:
  - `SalesAgent` (bucle de reanudación), `SalesToolNames` (tools que requieren confirmación), `ToolErrorCodes`, `AuditLog` (evento `ApprovalDecided`) y la descripción de `cancel_order`;
  - `AgentScenario`, `ScenarioCatalog`, `AgentRunner` y `RunnerHost`;
  - `docs/learn/05-…`, `06-…`, `04-…`, `README.md` y `glosario.md`, `docs/plan.md` y `README.md` raíz.
- **Dependencias**: ninguna nueva. `ApprovalRequiredAIFunction` y los contenidos de aprobación están en `Microsoft.Extensions.AI` 10.11.0, ya presente.
- **Comportamiento visible**:
  - una ejecución en la que el modelo pide `cancel_order` se detiene a preguntar en la consola;
  - sin consola interactiva se deniega;
  - el resto de escenarios no cambia.
- **Conceptos nuevos y dónde se documentan**:
  - documento 05: humano en el bucle con pausa y reanudación, aprobación ligada a la acción exacta, todo o nada, *fail closed* sin consola, y por qué mostrar los argumentos como JSON (inyección en la terminal);
  - glosario: aprobación fuera de banda, pausa y reanudación, *fail closed*.
