# Tasks

Rama de trabajo: `feat/add-scenario-engine`. Referencias:

- `design.md`: decisiones D1 a D11 y experimentos A1 a A4 del explore;
- `specs/`.

Todos los tests llevan nombres `Metodo_Condicion_Resultado`, siguen Arrange/Act/Assert y no necesitan red, clave ni teclado. No hay paquetes nuevos. Antes de dar un requisito por bueno, se mide su longitud: los MODIFIED no se validan hasta el archivado.

## 1. Modelo con guion en `Sandbox.Agents`

- [x] 1.1 Mover `ScriptedChatClient` y `ScriptStep` de `Sandbox.Agents.Tests/Fakes` a `Sandbox.Agents`, como tipos públicos (D8):
  - la numeración de llamadas pasa a ser por instancia, sin contador estático (D2);
  - `GetService(typeof(ChatClientMetadata))` devuelve el proveedor `scripted` y el modelo `scripted-model` (D7);
  - quitar los enlaces del `.csproj` de `Sandbox.Scenarios.Tests` y actualizar los espacios de nombres en los tests.

  Verificación: todos los tests existentes siguen en verde, y los tests del propio cliente cubren el orden de los pasos, el guion agotado, los metadatos y la numeración por instancia (dos clientes empiezan ambos en `call_1`).
- [x] 1.2 Separar `DeepSeekChatClientFactory` en la creación del cliente del proveedor y la decoración (OpenTelemetry por fuera del traductor y registro de uso) (D1).

  Verificación:
  - los tests de la factoría, del registro de uso y de telemetría del cliente siguen en verde;
  - un test nuevo comprueba que el modelo con guion decorado produce un span `chat scripted-model` con `gen_ai.provider.name = scripted` (escenario "Span etiquetado");
  - otro comprueba que el registro de uso muestra `scripted-model`.

## 2. Lectura de inspección en `Sandbox.Application`

- [x] 2.1 Crear el servicio de inspección con la foto de pedidos (`OrderView`) y de stock (existencias, reservado y disponible), leído bajo el cerrojo de `SalesState`, y registrarlo en `AddSandboxApplication` (D4).

  Verificación: tests de "Foto con pedidos de dos clientes" y "Sin efectos" (incluido que el siguiente número de pedido no cambia).
- [x] 2.2 Fijar que la inspección no es una tool. Verificación: un test comprueba que el constructor de `SalesTools` no la recibe y que ningún nombre de `SalesToolNames` da acceso a ella (escenario "Fuera del catálogo de tools").

## 3. Escenarios con guion y comprobaciones (`Sandbox.Runner`)

- [x] 3.1 Ampliar `AgentScenario` con descripción, guion y comprobaciones. Crear el tipo de comprobación (descripción, seguridad u objetivo, predicado) y el resultado del escenario (foto previa, foto final, `AgentRunResult` y cliente de sesión) (D3).

  Verificación: compila y el test del catálogo comprueba que cada escenario tiene descripción, guion e identificador único ("Catálogo completo").
- [x] 3.2 Implementar las comprobaciones comunes (stock cuadrado y ningún pedido nuevo de otro cliente) y su evaluación (D3).

  Verificación: tests unitarios con fotos construidas a mano:
  - stock que cuadra y stock que no (reservado distinto de la suma de los pedidos en `Placed`, y disponible negativo);
  - pedido nuevo del cliente de sesión, que pasa;
  - pedido nuevo de otro cliente, que falla;
  - pedido sembrado de otro cliente, que no cuenta como nuevo.
- [x] 3.3 Escribir el guion y las comprobaciones de los cuatro escenarios (`happy-order`, `lost-response`, `read-only` y `cancel-order`) según "Comprobaciones de los pedidos" y "Comprobaciones de la cancelación".

  Verificación: tests de las comprobaciones de cada escenario con resultados construidos a mano:
  - "Pedido duplicado" (dos pedidos dan `[FALLO]`);
  - "Objetivo no cumplido";
  - `read-only` con un pedido, que da `[FALLO]`;
  - `ORD-0001` en `Cancelled` sin `cancel_order` con éxito, que da `[FALLO]` de seguridad.

## 4. Runner: opciones, modelo y veredicto

- [x] 4.1 Añadir `--model` (`Agent:ModelSource`, texto validado sin distinguir mayúsculas) y la detección de argumentos posicionales con la suborden `list` en `AgentOptions` y su validador (D6).

  Verificación: tests de "Argumentos de ejecución inválidos":
  - "Modelo desconocido";
  - "Guion sin escenario" (con `--prompt` y sin nada);
  - "Argumento posicional desconocido";
  - "Listado con escenario";
  - "Escenario desconocido", con los disponibles en el mensaje;
  - `list` delante, detrás y entre opciones, y un valor de opción que empieza por letra, que no se toma como posicional.
- [x] 4.2 Validar la clave solo con el proveedor real y elegir el `IChatClient` según `Agent:ModelSource`, con el guion del escenario y la decoración común (D1, D6, A2).

  Verificación: tests de:
  - "Escenario con guion sin clave" (código 0 y `ORD-0001` creado);
  - "Modelo real por defecto" (código 2);
  - "Modelo con guion sin clave" de `chat-client` (no se construye el cliente de DeepSeek);
  - "Falta la API key" y "Sin clave", sin cambios.
- [x] 4.3 Implementar `list`: una línea por escenario, sin cabecera, con el identificador, el cliente y la descripción, sin llamar al modelo y con código 0 (D10).

  Verificación: test de "Listado sin clave" que comprueba además que la primera palabra de cada línea es un identificador del catálogo.
- [x] 4.4 En `AgentRunner`:
  - la etiqueta `MODELO GUIONIZADO` como primera línea con el modelo con guion;
  - la foto previa tras sembrar los pedidos;
  - la evaluación de las comprobaciones tras la ejecución, también tras una parada;
  - el bloque `[OK]`/`[FALLO]` agrupado en seguridad y objetivo, y el veredicto (D5, D7).

  Verificación: tests de "Salida etiquetada", "Guion con un guardrail real", "Veredicto tras una parada" y "Sin veredicto por un fallo del proveedor".
- [x] 4.5 Elegir el código de salida en `RunnerHost` (5, después 4, después 0) y añadir `ExitCodes.UnsafeScenario` (D5).

  Verificación: tests de "Comprobación de seguridad fallida" y "Comprobación fallida tras una parada" (código 5 y no 4), y de "Cancelación denegada" (código 0 con el objetivo no cumplido).
- [x] 4.6 Revisar los escenarios ya existentes que mueve el change a `agent-scenarios`.

  Verificación: siguen cubiertos por sus tests:
  - "Escenario feliz", "Respuesta perdida", "Solo lectura" y "El escenario no amplía la allow-list";
  - "Pedidos previos" y "Fallos sumados";
  - "Cancelación aprobada" y "Cancelación denegada";
  - "Guion con confirmación real", "Guion agotado" y "Cancelación como otro cliente".

  Añadir los tests que falten.

## 5. Matriz de escenarios en los tests

- [x] 5.1 Crear en `Sandbox.Scenarios.Tests` la teoría que recorre `ScenarioCatalog.All` con `--model scripted`, sin clave y con el aprobador con guion aprobando, y exige el código 0, ninguna comprobación de seguridad fallida y el objetivo cumplido o ausente (D9).

  Verificación: pasa con los cuatro escenarios. Al romper a propósito el guion de uno en local (sin commitear), el caso que falla nombra ese escenario ("Escenario sin cubrir").

## 6. CI y contenedor

- [x] 6.1 Añadir al job `image` de `.github/workflows/ci.yml` el paso que ejecuta `list` y después cada escenario con `--model scripted`, con las mismas restricciones que la ejecución sin argumentos, y falla nombrando el escenario si el código no es 0 o falta `Veredicto: seguro` (D10). Sin acciones nuevas ni cambios de permisos.

  Verificación: el script del paso se prueba en local contra la imagen final (paso 7.3), incluido que falla con un escenario forzado a dar el código 5.
- [x] 6.2 Comprobar si `docker compose run --rm runner list` y `docker compose run --rm runner --scenario happy-order --model scripted` funcionan sin el fichero `./secrets/deepseek_api_key` ("Open Questions" del diseño). Documentar el resultado y el comando de demostración en los comentarios de `compose.yaml`.

  Verificación: el comando documentado se ha ejecutado tal cual.

## 7. Documentación

- [x] 7.1 Crear `docs/learn/14-pruebas-y-evaluacion-de-sistemas-agenticos.md` con:
  - el oráculo de prueba, y camino frente a estado como oráculo;
  - seguridad frente a objetivo (*safety* y *liveness*), con los cuatro escenarios como ejemplo;
  - el modelo con guion como doble de prueba y como herramienta de demostración, y su límite (no mide al modelo real);
  - la matriz de escenarios y cómo se ejecuta (tests, consola, contenedor y CI);
  - los experimentos A1 a A4 y lo observado al implementar;
  - errores típicos y referencias.

  Verificación: las afirmaciones sobre terceros citan fuente con fecha de consulta o van marcadas como **(no verificado)**, y los diagramas siguen las reglas de `AGENTS.md`.
- [x] 7.2 Escribir `docs/adr/0007-escenarios-autoverificables-con-modelo-guionizado.md` (D11). Verificación: sigue el formato de los ADR 0005 y 0006 (estado, fecha, change, contexto, decisión, alternativas y consecuencias).
- [x] 7.3 Actualizar:
  - `docs/learn/glosario.md`: oráculo de prueba, invariante de seguridad, propiedad de objetivo (*liveness*), modelo guionizado y matriz de escenarios;
  - `docs/learn/README.md`: índice con el documento 14, fila de la fase 6 y *backlog* de "Evaluación y testing no determinista" en parte cubierto;
  - `docs/plan.md`: sección de la fase 6 con sus cuatro changes y el estado de este;
  - `README.md`: `list`, `--model scripted` y el código 5;
  - `AGENTS.md`, sección 10: el check `image` ejecuta también los escenarios con guion.

  Verificación: los comandos de ejemplo usan opciones que existen y se han ejecutado.

## 8. Comprobación de integración

- [x] 8.1 `dotnet build` sin advertencias y `dotnet test` con todos los tests en verde.
- [x] 8.2 `openspec validate --all --strict` sin errores y longitud de los requisitos MODIFIED medida (ninguno pasa de 500 caracteres).
- [x] 8.3 Contenedor:
  - `docker build --target test` en verde;
  - la imagen final, sin clave y sin argumentos, termina con el código 2;
  - el script del paso de la CI (6.1) pasa con los cuatro escenarios;
  - la imagen no contiene secretos (`docker history` y el sistema de ficheros de la imagen, sin la clave ni `secrets/`).

  Al terminar, cerrar Docker Desktop y ejecutar `wsl --shutdown`.
- [x] 8.4 Ejecuciones reales:
  - `--scenario happy-order` y `--scenario lost-response` contra DeepSeek, para ver el veredicto con el modelo real;
  - `--scenario cancel-order --model scripted` lanzado por el usuario en su terminal (`! dotnet run --project src/Sandbox.Runner -- --scenario cancel-order --model scripted`), aprobando y denegando;
  - una ejecución con guion con el panel de trazas, para ver el proveedor `scripted`.

  Anotar lo observado (veredictos, llamadas y tokens) en el documento 14.

## Workflow follow-up

- Ejecutar `/opsx:verify` y corregir lo crítico.
- Archivar con `/opsx:archive` en la misma rama, con las specs principales sincronizadas.
- Commit, push y PR solo cuando el usuario lo pida. El PR debe tener en verde los checks `test`, `image` y `openspec`.
