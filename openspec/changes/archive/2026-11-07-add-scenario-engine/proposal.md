# Proposal

## Why

La fase 6 de `docs/plan.md` (*Casos de prueba y escenarios*) pide una **matriz de escenarios documentada y ejecutable**. Hoy un escenario con nombre solo fija la preparación: cliente, petición, fallos, allow-list y pedidos previos. Lo que hace el modelo y lo que se comprueba al final viven sueltos en los tests, así que:

- desde el Runner solo se puede ejecutar contra DeepSeek, con clave y red, y nadie dice si el resultado es correcto: hay que interpretar el resumen a mano;
- con un modelo real **no se puede forzar un mal comportamiento**: para enseñar que la arquitectura contiene a un modelo que se equivoca o que es engañado, el modelo tiene que equivocarse, y nada garantiza que DeepSeek lo haga en el momento de la demostración;
- el responsable quiere que el producto final del laboratorio sirva para **hacer demostraciones al equipo** de lo verificado, y lo que solo vive en `dotnet test` no se puede enseñar.

Este change es el primero de los cuatro de la fase 6, según el explore del 2026-11-07:

1. **Motor de escenarios** (este change): modelo con guion, comprobaciones y veredicto, sobre los cuatro escenarios actuales.
2. Escenarios nuevos: stock agotado, pedido inexistente, pedido ajeno, fallo transitorio y fallo persistente.
3. *Prompt injection*, directa e indirecta.
4. Suite contra DeepSeek con repeticiones y tasa de veredictos.

## What Changes

- **Escenarios que se comprueban a sí mismos** (`Sandbox.Runner`). Cada escenario, además de su preparación, lleva:
  - una **descripción** legible;
  - un **guion**: las respuestas fijas que daría un modelo concreto, escritas en C# con los nombres de las tools;
  - **comprobaciones de seguridad**: invariantes que deben cumplirse haga lo que haga el modelo (por ejemplo, ningún pedido duplicado, o el stock cuadra con los pedidos vivos);
  - **comprobaciones de objetivo**: lo que se espera conseguir (por ejemplo, que el cliente tenga su pedido).
- **Veredicto tras cada escenario**:
  - el Runner escribe cada comprobación con `[OK]` o `[FALLO]` y un veredicto final;
  - nuevo código de salida **`5`** si falla alguna comprobación de seguridad, con prioridad sobre el `4`;
  - no cumplir el objetivo se informa, pero no cambia el código de salida.
- **Modelo con guion** (`--model scripted`):
  - sustituye solo al cliente del proveedor; el bucle, los guardrails, la aprobación, la auditoría y la telemetría son los reales;
  - no necesita clave ni red, también en el contenedor;
  - se identifica siempre: una línea `MODELO GUIONIZADO` en la salida, y el proveedor `scripted` en los spans y los registros;
  - con `--prompt` o sin escenario (saludo) da el código 2, porque no hay guion.
- **Suborden `list`**: lista los escenarios con su descripción y su cliente, sin llamar al modelo ni pedir la clave. Un argumento posicional desconocido da el código 2, y un `--scenario` desconocido lista los disponibles en el mensaje.
- **Lectura de inspección** (`Sandbox.Application`): una foto de todos los pedidos y del stock (existencias y reservas) para evaluar las comprobaciones. **Nunca se ofrece como tool**: el modelo solo ve lo de su cliente.
- **Un único modelo con guion**: pasa de los tests a `Sandbox.Agents`, y los tests reutilizan esa clase.
- **Matriz en los tests**: un test recorre el catálogo y ejecuta cada escenario con su guion. Exige que tenga guion y al menos una comprobación de seguridad, que el veredicto sea "seguro", y que el objetivo se cumpla cuando el escenario lo espera.
- **CI**: el check `image` ejecuta además los escenarios con el modelo con guion en la imagen final endurecida, sin red ni clave, y espera el código 0 y el veredicto seguro.
- **Documentación**:
  - documento nuevo `docs/learn/14-pruebas-y-evaluacion-de-sistemas-agenticos.md`;
  - ADR 0007 (escenarios autoverificables con modelo guionizado en el Runner);
  - glosario, índice y *backlog* de `docs/learn/README.md`, `docs/plan.md` (sección de la fase 6), `README.md` raíz y comentarios de `compose.yaml`.

### Fuera de alcance

- **Escenarios nuevos** y **prompt injection**: changes 2 y 3 de la fase.
- **Repeticiones contra DeepSeek y tasas** (`--repeat`): change 4.
- **Guiones reactivos**, que decidan según los resultados de las tools: el sistema es determinista y un guion fijo basta (design D2).
- **Guiones en ficheros de datos** (JSON o YAML) editables sin compilar.
- **Comparar el camino del agente** entre ejecuciones a partir de las trazas: se mide en el change 4 o en la fase 7.
- **Comprobaciones para `--prompt`**: un texto libre no tiene oráculo.
- **Cambios en el `Dockerfile`**.

## Capabilities

### New Capabilities

- `agent-scenarios`: los escenarios del agente como unidad ejecutable y verificable:
  - su definición (preparación, descripción, guion y comprobaciones);
  - el modelo con guion;
  - el veredicto;
  - el listado;
  - la matriz en los tests;
  - los cuatro escenarios actuales con sus comprobaciones.

### Modified Capabilities

- `sales-agent`: se **mueven** a `agent-scenarios` "Escenarios con nombre", "Pedidos previos de un escenario" y "Escenario de cancelación". `sales-agent` queda para el agente en sí.
- `runner-host`:
  - requisito nuevo, "Elección del modelo" (`--model deepseek|scripted`);
  - "Códigos de salida" (nuevo `5`);
  - "Argumentos de ejecución inválidos" (modelo desconocido, modelo con guion sin escenario, posicionales desconocidos, `list` con otras opciones y escenarios disponibles en el mensaje).

  La etiqueta del modelo con guion, el veredicto y el listado se especifican en `agent-scenarios`.
- `chat-client`, "Opciones de conexión validadas al arrancar": con el modelo con guion o con `list`, la API key no se exige y no se construye el cliente del proveedor.
- `order-operations`: requisito nuevo, "Lectura de inspección".
- `continuous-integration`, "Imagen final verificada": ejecución de los escenarios con el modelo con guion.

## Impact

- **Ficheros nuevos**:
  - en `src/Sandbox.Agents/`: el modelo con guion y su paso de guion, que salen de los tests;
  - en `src/Sandbox.Application/`: la lectura de inspección y su vista;
  - en `src/Sandbox.Runner/`: las comprobaciones, su evaluación y el veredicto, y el listado;
  - en los tests: la matriz del catálogo y los tests del modelo con guion, del veredicto, de la inspección y de las opciones nuevas;
  - `docs/learn/14-…` y `docs/adr/0007-…`.
- **Ficheros modificados**:
  - `DeepSeekChatClientFactory`, que se separa en proveedor y decoradores;
  - `RunnerHost`, `ExitCodes`, `AgentOptions` y su validador, `AgentScenario`, `ScenarioCatalog` y `AgentRunner`;
  - `DeepSeekOptionsValidator`;
  - los tests que usan el modelo con guion (cambio de espacio de nombres);
  - `.github/workflows/ci.yml` (paso nuevo en `image`);
  - `compose.yaml` (solo comentarios), `docs/plan.md`, `README.md`, `AGENTS.md` (sección 10, el check `image`) y `docs/learn/README.md` y `glosario.md`.
- **Dependencias**: ninguna nueva.
- **Comportamiento visible**:
  - los escenarios terminan con un bloque de comprobaciones y un veredicto;
  - `--model scripted` y `list` funcionan sin clave;
  - el código 5 es nuevo;
  - sin `--model`, todo se ejecuta contra DeepSeek como hasta ahora.
- **Conceptos nuevos y dónde se documentan**, en el documento 14:
  - oráculo de prueba;
  - comprobaciones de seguridad frente a objetivo (*safety* y *liveness*);
  - camino frente a estado como oráculo;
  - el modelo con guion como doble de prueba y como herramienta de demostración;
  - la matriz de escenarios.

  Se añaden al glosario.
