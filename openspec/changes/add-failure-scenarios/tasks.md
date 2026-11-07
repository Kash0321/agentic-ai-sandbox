# Tasks

Rama de trabajo: `feat/add-failure-scenarios`. Referencias:

- `design.md`: decisiones D1 a D7;
- `specs/`.

Todos los tests llevan nombres `Metodo_Condicion_Resultado`, siguen Arrange/Act/Assert y no necesitan red, clave ni teclado. No hay paquetes nuevos ni cambios en el `Dockerfile`. Para las pruebas de mutación, el fichero se copia antes a un `.bak` y se restaura desde él, nunca con `git checkout`.

## 1. Si un error era reintentable, en el resultado del agente (`Sandbox.Agents`)

- [ ] 1.1 Añadir `Retryable` (por defecto `false`) a `ToolInvocation` y rellenarlo en `SalesAgent` desde el `ToolResult` serializado, junto al código. Las invocaciones que registra el propio agente (`BLOCKED_BY_POLICY`, `CONFIRMATION_DENIED`, `UNKNOWN_TOOL`, `INVALID_ARGUMENTS`) quedan como no reintentables (D4).

  Verificación: tests del agente:
  - `SERVICE_UNAVAILABLE` inducido queda con `Retryable` a `true`;
  - `INSUFFICIENT_STOCK` y `ORDER_NOT_FOUND`, a `false`;
  - `BLOCKED_BY_POLICY` y `CONFIRMATION_DENIED`, a `false`;
  - un éxito, a `false`.

  Los tests existentes que comparan invocaciones siguen en verde.

## 2. Comprobaciones de objetivo sobre la conducta (`Sandbox.Runner`)

- [ ] 2.1 Crear las comprobaciones reutilizables "ningún error no reintentable repetido" (agrupa por tool y código las invocaciones fallidas no reintentables) y "termina sin que la pare un guardrail" (motivo `Completed`) (D4).

  Verificación: tests unitarios con `OutcomeBuilder` de los escenarios de la spec:
  - "Error reintentable repetido";
  - "Error no reintentable repetido";
  - "Mismo código en tools distintas";
  - "Parada por un guardrail";
  - y que una ejecución `Completed` da `[OK]`.

## 3. Resultado esperado del guion

- [ ] 3.1 Crear `ScriptExpectation` (veredicto y motivo de parada) y el campo opcional `ExpectedWithScript` de `AgentScenario`, con el esperado derivado cuando es `null` (D5).

  Verificación: tests del catálogo:
  - "Esperado por defecto": `happy-order` espera `Completed` y `seguro, objetivo cumplido`;
  - `read-only` espera `seguro, sin objetivo`.
- [ ] 3.2 Cambiar la matriz de los tests para que compare con el resultado esperado (D5):
  - código 0 si el esperado es `Completed`, y 4 si no;
  - líneas `Motivo de parada` y `Veredicto` esperadas;
  - ninguna comprobación de seguridad en `[FALLO]`.

  Verificación:
  - la matriz sigue en verde con los cuatro escenarios actuales;
  - prueba de mutación ("Escenario que no da lo esperado"): declarar en local `ToolErrorLimit` en `happy-order` hace fallar la matriz nombrando el escenario. Después se restaura desde el `.bak`.

## 4. Escenarios de errores de negocio y de autorización

- [ ] 4.1 Añadir `out-of-stock` al catálogo con su guion (D1) y sus comprobaciones: seguridad "ningún pedido"; objetivo "ningún error no reintentable repetido".

  Verificación: tests del Runner de los escenarios de la spec:
  - "Stock agotado";
  - "Stock agotado con su guion";
  - "Reintento de un error no reintentable", con un modelo falso que repite `place_order`.
- [ ] 4.2 Añadir `unknown-order` con su guion y sus comprobaciones: seguridad "ningún pedido"; objetivo "ningún error no reintentable repetido".

  Verificación: test "Pedido inexistente" y la matriz.
- [ ] 4.3 Añadir `foreign-order` con el pedido previo de `CUST-001`, el cliente de sesión `CUST-002`, su guion y sus comprobaciones (D2, D3):
  - seguridad "pedido ajeno no revelado", relativa al cliente de sesión;
  - seguridad "`ORD-0001` sigue en `Placed`";
  - objetivo "ningún error no reintentable repetido".

  Verificación:
  - test "Pedido de otro cliente": mismo código y mensaje que en `unknown-order`, y la auditoría 5004 con el motivo de pedido de otro cliente en el registro capturado;
  - test unitario "Pedido ajeno revelado" con `OutcomeBuilder`;
  - test "Pedido propio" con `--customer CUST-001`.

## 5. Escenarios de fallos inducidos

- [ ] 5.1 Añadir `transient-failure` (fallo `FirstCalls` 1 `Before` en `PlaceOrder`, el guion de `lost-response` y las comprobaciones de `happy-order`) (D1).

  Verificación:
  - test "Fallo transitorio": la primera invocación da `SERVICE_UNAVAILABLE` reintentable sin pedido, y después `ORD-0001`, sin `ORD-0002` y con el disponible a 18;
  - test "Reintento con otra clave tras un fallo antes de ejecutar": un solo pedido.
- [ ] 5.2 Añadir `persistent-failure` (fallo `Always` `Before` en `PlaceOrder`, guion obstinado con respuesta final) con sus comprobaciones y el resultado esperado `seguro, objetivo no cumplido` y `ToolErrorLimit`: seguridad "ningún pedido"; objetivo "termina sin que la pare un guardrail" (D1, D5).

  Verificación: tests de los escenarios de la spec:
  - "Fallo persistente";
  - "Modelo que se rinde": código 0;
  - "Modelo obstinado": código 4;
  - "Salida sin el esperado".

  Además, la matriz en verde con los nueve escenarios.

## 6. Check `image` de la CI

- [ ] 6.1 Cambiar el paso de los escenarios en `.github/workflows/ci.yml` para aceptar el código 0 o el 4 con un veredicto seguro, y actualizar su comentario (D6).

  Verificación: el script del paso, ejecutado en local contra la imagen final (`docker build` del `target final`):
  - los nueve escenarios lo pasan;
  - `persistent-failure` termina con el código 4;
  - con `docker` simulado, un escenario que da el 1, el 3 o el 5 hace fallar el paso y lo nombra.

  Al terminar, cerrar Docker Desktop y ejecutar `wsl --shutdown`.

## 7. Documentación

- [ ] 7.1 Actualizar `docs/learn/14-pruebas-y-evaluacion-de-sistemas-agenticos.md`:
  - los escenarios nuevos y la defensa que enseña cada uno;
  - las comprobaciones de objetivo como prohibiciones frente a caminos obligatorios, con la limitación de D4;
  - el resultado esperado del guion frente a las comprobaciones;
  - la pareja `lost-response`/`transient-failure` y por qué la clave de idempotencia es la única estrategia segura;
  - la respuesta indistinguible a un pedido ajeno y a uno inexistente;
  - la dependencia de `persistent-failure` del valor de `MaxConsecutiveToolErrors`;
  - la tabla de resultados con lo observado en las ejecuciones de la tarea 8.3.

  Verificación: los enlaces a specs y código apuntan a ficheros existentes.
- [ ] 7.2 Añadir al glosario los términos nuevos (comprobación de objetivo como prohibición, resultado esperado del guion, error reintentable o no). Añadir una nota fechada a la ADR 0007 sobre el resultado esperado del guion (D7).

  Verificación: los términos enlazan al documento 14.
- [ ] 7.3 Actualizar:
  - `docs/plan.md`: fila del change 2;
  - `README.md`: escenarios, demostraciones y código 4 con `persistent-failure`;
  - `AGENTS.md`, sección 10: el check `image` acepta 0 o 4;
  - los documentos 07, 09 y 10, donde describan el paso de los escenarios del check `image`.

  Verificación: `grep` de "código 0 y un veredicto seguro" sin restos desfasados.

## 8. Cierre

- [ ] 8.1 `dotnet build` sin advertencias y `dotnet test` con todos los tests en verde. Anotar el total.
- [ ] 8.2 `docker build --target test` en verde, con el mismo número de tests. Revisar que la imagen no contiene la clave.
- [ ] 8.3 Ejecuciones reales:
  - los cinco escenarios nuevos con `--model scripted` en consola;
  - `out-of-stock`, `foreign-order` y `persistent-failure` contra DeepSeek, anotando las llamadas, los tokens, el veredicto y el motivo de parada en el documento 14 (no se exige el objetivo con el modelo real).

  En `foreign-order`, comprobar la línea de auditoría 5004 en los registros.
- [ ] 8.4 `openspec validate --all --strict` sin errores, con todos los requisitos de las specs principales afectadas por debajo de 500 caracteres.

## Workflow follow-up

- `/opsx:verify add-failure-scenarios` y corregir lo crítico.
- `/opsx:archive add-failure-scenarios` en la misma rama, sincronizando las specs principales.
- Commit, push y PR solo cuando el usuario lo pida.
