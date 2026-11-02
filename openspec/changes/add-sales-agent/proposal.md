# Proposal

## Why

Las fases 2 y 3 dejaron un cliente de chat hacia DeepSeek y un dominio de ventas con reglas, idempotencia y fallos inducidos, pero el modelo todavía no puede actuar sobre ese dominio. Este change cubre la **fase 4** de `docs/plan.md` (*Pipeline agéntico*):

- tools (`AIFunction`) que devuelven `ToolResult<T>`;
- el bucle de invocación de funciones (`UseFunctionInvocation`);
- un system prompt;
- trazas;
- el Runner ejecutando escenarios.

Su criterio de salida es un escenario feliz de extremo a extremo: crear un pedido.

El change parte de un explore con un spike contra DeepSeek real (18 llamadas, recogidas en `design.md`). Hallazgos que condicionan el alcance:

- **El modelo reutiliza la clave de idempotencia al reintentar** tras una respuesta perdida (fallo `After` del inyector): ocurrió en 2 de 2 ejecuciones, y solo se creó un pedido. Por eso la clave es un parámetro que rellena el modelo.
- **El nombre por defecto de una tool es el del método sin `Async`** (`PlaceOrder`). Los nombres se fijan de forma explícita para que el prompt y las descripciones puedan citarlos sin riesgo de que no coincidan.
- **`response.Text` mezcla los textos intermedios con la respuesta final**, así que el Runner muestra solo el último mensaje del asistente.
- **Con el razonamiento (*thinking*) y tools activados a la vez**, la documentación de DeepSeek exige reenviar `reasoning_content`, so pena de un 400. El adaptador de `Microsoft.Extensions.AI.OpenAI` no lo reenvía, y aun así la API respondió 200. Esa combinación queda sin soporte en esta fase, con un aviso.

## What Changes

- **Aplicación**: nueva query `ListProducts` (todos los productos con su disponible), para que el agente descubra los SKU a partir de nombres ("dos tazas rojas"). El inyector de fallos admite la operación `ListProducts`.
- **Tools** (`Sandbox.Agents`): `list_products`, `get_stock`, `get_customer`, `get_order`, `place_order` y `cancel_order`.
  - Funcionan sobre un **cliente de sesión** que fija el Runner: ninguna recibe el identificador del cliente.
  - `place_order` y `cancel_order` reciben una clave de idempotencia que rellena el modelo.
  - Devuelven `ToolResult<T>` con `ToolError(Code, Message, Retryable)`; `Retryable` solo es verdadero en los errores transitorios.
  - Nunca lanzan excepciones hacia el modelo: lo inesperado se convierte en `INTERNAL_ERROR`, y la cancelación se propaga.
  - Cada invocación se registra con su nombre, su duración y su resultado, sin argumentos.
  - `ShipOrder` **no** se expone: enviar un pedido es una acción de almacén, no del cliente.
- **Agente** (`Sandbox.Agents`):
  - system prompt en español, que orienta el uso de las tools y la clave de idempotencia pero no contiene reglas de negocio;
  - bucle de invocación con los límites por defecto de `FunctionInvokingChatClient`;
  - resultado de cada ejecución: respuesta final, llamadas al modelo, tools invocadas con su resultado y tokens;
  - aviso si se activa el razonamiento junto con las tools.
- **Runner**:
  - además del saludo (sin argumentos, sin cambios), acepta `--scenario <id>` o `--prompt "texto"`, nunca los dos, y `--customer` para el cliente de sesión (por defecto `CUST-001`);
  - escenarios con nombre `happy-order` (crear un pedido) y `lost-response` (el mismo pedido con la primera respuesta perdida);
  - por la salida estándar, la respuesta final y un resumen; los textos intermedios, al registro;
  - un escenario desconocido o una combinación de argumentos inválida termina con el código 2.
- **Tests**:
  - un cliente de chat falso **con guion** (llamadas a tools turno a turno), sin red;
  - tests de cada tool: caso feliz, cada código de error y esquema;
  - tests del agente y de los dos escenarios de extremo a extremo.
- **Documentación**:
  - documento nuevo `docs/learn/12-diseno-de-tools-para-llms.md`;
  - documentos 02 y 03 actualizados con las trazas reales;
  - ADR 0004 sobre la identidad del cliente fuera del contrato de las tools;
  - glosario, índice, `docs/plan.md` y `README.md`.

### Fuera de alcance

- **Guardrails** (fase 5): límites de iteraciones y de tokens propios, allow-list formal, confirmación de acciones destructivas, auditoría y OpenTelemetry. Esta fase deja los valores por defecto de `FunctionInvokingChatClient` (40 iteraciones y 3 errores seguidos).
- **Autorización** (fase 5): que el cliente de sesión no pueda leer pedidos de otros clientes (`FORBIDDEN`). En esta fase, `get_order` devuelve cualquier pedido existente.
- **Matriz de escenarios** (fase 6): stock insuficiente, pedido inexistente, prompt injection, acción destructiva y suite contra DeepSeek real.
- **Reenviar `reasoning_content`** con razonamiento y tools: se documenta y se avisa, pero no se implementa.
- **Streaming**, conversaciones de varios turnos o interactivas, y memoria entre ejecuciones.
- **Tool `ship_order`** y queries de listado de pedidos (`ListOrders`).
- **Cambios en el `Dockerfile`, en compose o en la CI**: sin argumentos, el Runner sigue haciendo el saludo y el check `image` sigue esperando el código 2.

## Capabilities

### New Capabilities

- `agent-tools`: el contrato de las tools que ve el modelo:
  - nombre, parámetros, descripciones, resultado y códigos de error de cada tool;
  - `ToolResult`/`ToolError` y la traducción de los errores de dominio;
  - el cliente de sesión;
  - el borde que captura lo inesperado;
  - el registro de cada invocación y la serialización de los resultados.
- `sales-agent`: el agente que usa esas tools:
  - el system prompt y el bucle de invocación;
  - el resultado de una ejecución (respuesta final, llamadas, tools y tokens);
  - los escenarios con nombre;
  - el aviso de razonamiento con tools.

### Modified Capabilities

- `order-operations`: nueva query `ListProducts`.
- `fault-injection`, "Reglas por operación": la lista de operaciones incluye `ListProducts`.
- `runner-host`: nuevo requisito para ejecutar un escenario o un texto libre con un cliente de sesión, con la salida y los códigos de error de los argumentos.

## Impact

- **Ficheros nuevos**:
  - en `src/Sandbox.Agents/`: las tools y su contrato, el agente y el system prompt como recurso incrustado;
  - en `src/Sandbox.Runner/`: las opciones de ejecución y el catálogo de escenarios;
  - en `tests/Sandbox.Agents.Tests/`: el cliente falso con guion y los tests de tools y agente;
  - en `tests/Sandbox.Scenarios.Tests/`: los tests de extremo a extremo;
  - `docs/learn/12-diseno-de-tools-para-llms.md` y `docs/adr/0004-…`.
- **Ficheros modificados**:
  - `ISalesQueries`, `SalesQueries`, el decorador de queries y `FaultOperations` (`Sandbox.Application`);
  - `RunnerHost` y el registro de servicios del Runner;
  - `docs/learn/02-…`, `03-…`, `README.md` y `glosario.md`, `docs/plan.md` y `README.md` raíz.
- **Dependencias**: ninguna nueva. Se usan `Microsoft.Extensions.AI` y `.AI.OpenAI` 10.11.0, ya presentes, y el codificador JSON de la biblioteca base.
- **Comportamiento visible**: sin argumentos, nada cambia. Con `--scenario` o `--prompt`, el Runner llama al modelo varias veces por ejecución: entre 3.600 y 6.700 tokens de entrada por escenario, según el spike.
- **Conceptos nuevos y dónde se documentan**:
  - documento 12 (nuevo): diseño de tools para LLMs (nombres, descripciones, granularidad, esquemas, salidas compactas, errores accionables, idempotencia en manos del modelo);
  - documentos 02 y 03: el bucle agéntico y el protocolo de *function calling* con trazas reales (llamadas en paralelo, textos intermedios, tokens por llamada);
  - ADR 0004: la identidad del cliente no la decide el modelo;
  - glosario: los términos nuevos.
