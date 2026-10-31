# Tasks

Rama de trabajo: `feat/add-ci-pipeline`. Referencias: `design.md` (decisiones D1 a D11 y consultas del explore) y `specs/`.

- **Push y PR**: por D11, el apply puede publicar la rama y abrir un **PR en borrador** desde el grupo 3. Pasa a "listo para revisión" solo tras verify y archive.
- **Cambios temporales**: los de las pruebas negativas se hacen con commits que se revierten en un commit posterior, y nunca quedan en el estado final de la rama.
- **Docker**: hace falta Docker Desktop solo en el grupo 2. Al terminar: salir de Docker Desktop y ejecutar `wsl --shutdown`.

## 1. Versiones fijadas

- [x] 1.1 Reverificar en la fuente primaria y anotar aquí la fecha, el SHA completo y la versión de cada pieza:
  - `actions/checkout`, `docker/setup-buildx-action`, `docker/build-push-action` y `anchore/scan-action`: `releases/latest` y SHA del commit de su etiqueta (resolviendo las etiquetas anotadas);
  - Grype: `releases/latest` de `anchore/grype`;
  - Node LTS en `nodejs.org/dist/index.json` y digest de la lista de manifiestos de `node:<versión>-alpine` en Docker Hub;
  - `@fission-ai/openspec` en npm: debe seguir siendo la versión instalada en local (`openspec --version`);
  - que ninguna versión elegida sea preliminar.

  Si algo ha cambiado respecto al explore, usar la versión nueva y anotarlo.

  **Reverificado el 2026-10-31 a las 17:16 UTC**, sin cambios respecto al explore y ninguna versión preliminar:

  | Pieza | Versión | SHA o digest |
  |---|---|---|
  | `actions/checkout` | v7.0.1 | `3d3c42e5aac5ba805825da76410c181273ba90b1` |
  | `docker/setup-buildx-action` | v4.4.1 | `f87e5991a6d7451dcb8d9637bfbc97413f497069` |
  | `docker/build-push-action` | v7.4.0 | `c3c9e263c25d99ce0380d002d59b67737d91b0dc` |
  | `anchore/scan-action` | v7.4.2 | `27805bf3b4e84b4a5c980df22ed233c00390a439` |
  | Grype | v0.121.0 | — (se fija por versión en `grype-version`) |
  | Node LTS | 26.13.0 (*Lithium*, 2026-10-28) | `node:26.13.0-alpine@sha256:c6ad9e9c1d80f6eb607c26b4b0a132ff7a2344fad1c78ea3baa2a7d4c8de6d94` (índice OCI multiplataforma) |
  | `@fission-ai/openspec` | 1.16.0 (la misma que en local) | — |

## 2. Imagen de herramientas de OpenSpec

- [x] 2.1 Crear `tools/openspec/Dockerfile` según D5. Verificar:
  - `git check-attr eol -- tools/openspec/Dockerfile` devuelve `lf`;
  - `docker build -t openspec-cli tools/openspec` termina bien;
  - `docker run --rm --network none -v "${PWD}:/repo:ro" openspec-cli validate --all --strict` da el mismo resultado que `openspec validate --all --strict` en local;
  - `docker image inspect` muestra un usuario distinto de root.
- [x] 2.2 Prueba negativa en local: un requisito temporal sin escenario en el change hace fallar la ejecución en contenedor con código distinto de 0 y nombra el requisito. Revertir y comprobar `git status`.
- [x] 2.3 Comprobar si la CLI intenta conexiones de red o escribe fuera de `HOME`: buscar errores o avisos en la salida sin red. Anotar lo observado en `design.md` (D5, telemetría **(no verificado)**).

## 3. Workflow de CI

- [x] 3.1 Crear `.github/workflows/ci.yml` según D1 a D5:
  - disparadores `pull_request` y `push` a `main`, sin filtros de ruta;
  - `permissions: contents: read`;
  - `concurrency`, `timeout-minutes` y runner `ubuntu-24.04`;
  - jobs `test`, `image` y `openspec`;
  - acciones por SHA con comentario de versión;
  - `persist-credentials: false`.

  Verificar en local que el YAML es válido (`docker run` de una imagen de `actionlint` fijada por digest, o revisión manual si no se usa) y que no contiene `secrets.`, `pull_request_target` ni referencias `@v`.

  **Hecho el 2026-10-31**: `rhysd/actionlint:1.7.12@sha256:b1934ee5f1c509618f2508e6eb47ee0d3520686341fec936f3b79331f9315667` (última versión estable, ejecutado sin red) no encuentra ningún problema en `ci.yml` ni en `measure.yml`. Ninguna coincidencia de `secrets.`, `pull_request_target` ni `@v`, y las 10 referencias `uses:` usan un SHA de 40 caracteres con comentario de versión.
- [x] 3.2 Publicar la rama y abrir el PR en borrador (D11). Verificar en el PR:
  - aparecen los checks `test`, `image` y `openspec`, los tres en verde;
  - el registro de `test` muestra los tests de los tres proyectos;
  - el de `image` muestra la ejecución con código 0 y la tabla de Grype;
  - el de `openspec` muestra la validación estricta.

  Anotar los tiempos de cada job, en frío y en una segunda ejecución con caché.

  **Hecho el 2026-10-31, PR #13** (en borrador). Los tres checks salieron en verde a la primera, solo con el evento `pull_request`: el push a la rama no dispara la CI.
  - `test` ejecuta `Sandbox.Agents.Tests`, `Sandbox.Scenarios.Tests` y `Sandbox.Domain.Tests` (`total: 8`, `succeeded: 8`).
  - `image`:
    - la ejecución endurecida termina con 0;
    - Grype detecta 11 paquetes y 7 vulnerabilidades en `libc6` (6 de severidad media y 1 *negligible*), **ninguna con arreglo**;
    - el control las ignora por `only-fixed` y pasa.
  - `openspec`: 4 de 4 válidos.

  Tiempos de los jobs:

  | Job | En frío (run 41224370017) | Con caché, sin cambios de código (run 41224648076) |
  |---|---|---|
  | `test` | 51 s | 14 s (todos los pasos `CACHED`, también el de los tests) |
  | `image` | 2 min 20 s | 1 min 36 s |
  | `openspec` | 13 s | 14 s |

  Ajuste tras la primera ejecución (commit `328b3b4`): `cache-db: true` en los dos pasos de Grype, porque la base de datos se descargaba dos veces (unos 80 s cada una). Además, `severity-cutoff: high` en el informe, que anotaba un aviso engañoso por las vulnerabilidades medias. En la segunda ejecución, el paso de control restaura la base de datos de la caché (`Cache hit for: grype-db-v0.121.0`).
- [x] 3.3 Comprobar los permisos efectivos: la sección "GITHUB_TOKEN Permissions" del registro de cada job solo muestra `Contents: read` (y `Metadata: read`). Comprobar que la caché `type=gha` funciona con esos permisos: la segunda ejecución reutiliza capas (`CACHED`). Resuelve la marca **(no verificado)** de D2.

  **Hecho**:
  - en los tres jobs, "GITHUB_TOKEN Permissions" muestra solo `Contents: read` y `Metadata: read`;
  - con esos permisos, la caché de BuildKit se importa y se exporta: en la segunda ejecución, los pasos 7 a 21 de `test` salen `CACHED`;
  - la caché de la base de datos de Grype también funciona.

  También se observó que `docker/build-push-action` sube un artefacto con el registro del build (`.dockerbuild`, unos 37 KB). Su contenido no se ha revisado **(no verificado)**, pero este build no recibe ningún secreto.
- [x] 3.4 Pruebas negativas en el PR, cada una con un commit temporal revertido después:
  - un test que falla deja `test` en rojo con el nombre del test;
  - un `Program.cs` del Runner que devuelve 1 deja `image` en rojo;
  - un requisito sin escenario deja `openspec` en rojo;
  - `severity-cutoff: low` sin `only-fixed` muestra si el escaneo bloquea con los hallazgos actuales. Si no hay ninguno, anotarlo como **(no verificado)** en la política.

  **Ronda A** (commit temporal `f668982`, run 41224942023): los tres checks en rojo.
  - `test` falla y nombra `Sandbox.Domain.Tests.TemporaryFailingTests.Ci_WithFailingTest_TurnsCheckRed` (`total: 9`, `failed: 1`).
  - `openspec` falla con `ADDED "Requisito temporal sin escenario" must include at least one scenario`.
  - `image`, con `severity-cutoff: low` y sin `only-fixed`, falla por las 7 vulnerabilidades de `libc6` (`Failed minimum severity level`): el control bloquea cuando su umbral se alcanza.

  El caso exacto "alta o crítica **con arreglo**" no se puede reproducir hoy: la imagen no tiene ninguna. Queda **(no verificado)** con datos reales; lo que sí está probado es el mecanismo y que las vulnerabilidades sin arreglo no bloquean (3.2).

  Revertido en `2420464` y `d54d575`. El segundo commit hizo falta porque un `git add` con una ruta ya borrada abortó y dejó fuera el spec. La rama vuelve a ser idéntica a `328b3b4`, salvo `design.md` y `tasks.md`.

  **Ronda B** (commit temporal `f8ba7e6`, run 41225654497, ya con el ruleset de 6.2 aplicado):
  - `Program.cs` con `return 1;` deja `image` en rojo en el paso de ejecución endurecida ("Process completed with exit code 1");
  - `test` y `openspec` quedan en verde.

  Revertido en `79f15f4`, sin diferencias con `d69e433`.
- [x] 3.5 Comprobar el escenario "PR que solo cambia documentación": un commit del propio change que solo toque Markdown dispara los tres checks.

  **Hecho**: el commit `b522923` solo modifica ficheros `.md` y dispara `test`, `image` y `openspec`, los tres en verde (run 41226191089, evento `pull_request`).

## 4. Workflow manual de medición

- [x] 4.1 Crear `.github/workflows/measure.yml` según D6: solo `workflow_dispatch`, `contents: read`, acciones por SHA, y la tabla de tiempos con la información del runner en `$GITHUB_STEP_SUMMARY`. Verificar que no aparece entre los checks del PR.

  **Hecho**: actionlint sin problemas. Los checks del PR #13 son solo `test`, `image` y `openspec`.
- [x] 4.2 Intentar lanzarlo en la rama con `gh workflow run measure.yml --ref feat/add-ci-pipeline`.
  - Si funciona, verificar que el resumen muestra los cinco tiempos.
  - Si GitHub lo rechaza porque el workflow aún no está en `main`, que es lo que se espera según D11, anotar el mensaje exacto en `design.md` (D6). La medición pasa entonces al seguimiento tras el merge.

  **Resultado**: rechazado con `HTTP 404: workflow measure.yml not found on the default branch`, anotado en D6. La medición pasa al seguimiento.
- [x] 4.3 Documentación en `docs/learn/07-contenedores-en-sistemas-agenticos.md`:
  - actualizar la sección "CI" con lo implementado;
  - explicar cómo se lanza la medición;
  - si 4.2 la ejecutó, añadir sus tiempos junto a los del equipo local, como una sola ejecución, y la conclusión según el criterio del documento; si no, dejar la tabla preparada y marcada como pendiente;
  - revisar las marcas **(no verificado)** que se resuelvan.

## 5. Dependabot

- [x] 5.1 Crear `.github/dependabot.yml` según D7. Verificar tras el push:
  - la pestaña *Insights > Dependency graph > Dependabot* (o `gh api`) no muestra errores de configuración;
  - si GitHub publica un check de validación de `dependabot.yml` en el PR, está en verde.

  Anotar lo que se pudo comprobar antes del merge.

  **Comprobado antes del merge (2026-10-31)**:
  - GitHub **no** publica ningún check de validación de `dependabot.yml` en el PR: solo aparecen `test`, `image` y `openspec`. Dependabot lee la configuración de la rama por defecto, así que lo que muestre la pestaña de Dependabot solo se verá tras el merge (seguimiento).
  - Validación alternativa con el esquema público de SchemaStore (`dependabot-2.0.json`, borrador 7) y `ajv-cli` 5.0.0 en `node:26.13.0-alpine` fijada por digest, en el scratchpad:
    - el fichero real es válido;
    - una copia con `interval: fortnightly` se rechaza (`/updates/0/schedule/interval`).

    El esquema de SchemaStore no es la fuente oficial de GitHub, así que esto confirma la forma, no que GitHub la acepte.

## 6. Configuración de GitHub (orden de D8)

- [x] 6.1 Activar la exigencia de SHA (`gh api -X PUT repos/{owner}/{repo}/actions/permissions` con `enabled`, `allowed_actions` y `sha_pinning_required: true`). Verificar:
  - `gh api …/actions/permissions` devuelve `sha_pinning_required: true`;
  - la CI se reejecuta en verde;
  - un commit temporal con una acción fijada por etiqueta hace que GitHub rechace el workflow. Revertirlo.

  **Hecho el 2026-10-31**, con la aprobación expresa del responsable para el grupo 6:
  - la API pasa de `"sha_pinning_required":false` a `true`;
  - con el commit temporal `7b10893`, `ci` sale en verde (run 41225453954);
  - el workflow temporal con `actions/checkout@v7.0.1` falla en *Set up job* con la anotación "The action actions/checkout@v7.0.1 is not allowed in Kash0321/agentic-ai-sandbox because all actions must be pinned to a full-length commit SHA" (run 41225453987);
  - borrado en `9bff522`.
- [x] 6.2 Leer el `integration_id` real de los checks del PR (`gh api repos/{owner}/{repo}/commits/<sha>/check-runs`, campo `app.id`). Añadir a `.github/rulesets/protect-main.json` la regla `required_status_checks` con `test`, `image` y `openspec`, ese `integration_id` y `strict_required_status_checks_policy: false`. Aplicarlo con `gh api -X PUT repos/{owner}/{repo}/rulesets/26267641 --input .github/rulesets/protect-main.json`. Verificar:
  - `gh api …/rulesets/26267641` coincide con el JSON;
  - el PR muestra los tres checks como obligatorios;
  - durante una prueba negativa de 3.4 (repetir una), GitHub impide fusionar el PR.

  **Hecho el 2026-10-31** (commit `d69e433`, aplicado con `gh api -X PUT`):
  - Todos los *check runs* tienen `app.id=15368` (`app.slug: github-actions`).
  - El ruleset real coincide con el JSON, salvo valores por defecto que añade el servidor y que ya estaban antes del cambio: `required_reviewers: []`, `require_extra_approval_for_unattributed_changes: true` y `do_not_enforce_on_create: false`.
  - En el PR #13, GraphQL devuelve `isRequired: true` para `test`, `image` y `openspec`.
  - Con `image` en rojo (ronda B de 3.4), el PR queda en `mergeStateStatus: BLOCKED`. Al volver a verde pasa a `CLEAN`, aunque sigue en borrador, así que el bloqueo se debía al check obligatorio.
- [x] 6.3 Activar las alertas de vulnerabilidades y las actualizaciones de seguridad (`PUT …/vulnerability-alerts` y `PUT …/automated-security-fixes`). Verificar con `GET` que ambas están activas (204 y `enabled: true`).

  **Hecho el 2026-10-31**:
  - `GET …/vulnerability-alerts` pasa de `404` a `204 No Content`;
  - `GET …/automated-security-fixes` pasa de `{"enabled":false}` a `{"enabled":true,"paused":false}`;
  - `security_and_analysis.dependabot_security_updates` queda en `enabled`.

## 7. Documentación

- [x] 7.1 Crear `docs/adr/0002-checks-obligatorios-y-politica-de-vulnerabilidades.md` (D9): contexto, decisión, alternativas (Trivy, Scout, solo informar, bloquear todo) y consecuencias.
- [x] 7.2 Crear `docs/learn/09-integracion-continua-y-cadena-de-suministro.md`, con fecha de verificación y lo observado en los grupos 2 a 6. Debe cubrir:
  - qué es la CI con GitHub Actions y cómo funciona aquí;
  - el `GITHUB_TOKEN` y sus permisos;
  - `pull_request` frente a `pull_request_target`;
  - la fijación por SHA y el ataque de reetiquetado de marzo de 2026, como fuente secundaria;
  - los checks obligatorios en rulesets y la trampa de los filtros;
  - Dependabot: versiones frente a seguridad, grupos y periodo de espera;
  - el escaneo de imágenes con Grype y su resultado no determinista;
  - la caché de BuildKit en la CI;
  - la configuración que no vive en ficheros y cómo comprobarla;
  - errores típicos y referencias.

  Diagramas solo con caracteres seguros. Verificar con `check-docs.ps1` que no hay enlaces rotos ni flechas no monoespaciadas.
- [x] 7.3 Actualizar:
  - `docs/learn/glosario.md` con los términos nuevos;
  - `docs/learn/README.md`: índice, fila de la fase 1 y backlog (marcar "cadena de suministro", "builds reproducibles" y "escaneo de imágenes" según lo cubierto; proponer temas nuevos);
  - `README.md`: sección de CI y estructura con `.github/` y `tools/`;
  - `AGENTS.md`: secciones 8, 10 y 11, que hablan de la CI en futuro;
  - `docs/plan.md`: estado de `add-container-build` ("Fusionado en `main`") y de `add-ci-pipeline`.

  Verificar enlaces y flechas con `check-docs.ps1`.

## 8. Integración y cierre

- [x] 8.1 Validar y cerrar:
  - `openspec validate --all --strict` sin errores, en local y en el check `openspec`;
  - `dotnet build` sin advertencias y `dotnet test` en verde en local;
  - `docker build --target test .` en verde en local y en el check `test`;
  - los tres checks en verde en el último commit del PR;
  - revisión de que ningún fichero versionado contiene secretos ni rutas locales, y de que ningún workflow usa `secrets.`.

  **Hecho el 2026-10-31**:
  - `openspec validate --all --strict`: 4 de 4, en local y en el check `openspec`;
  - `dotnet build`: 0 advertencias y 0 errores;
  - `dotnet test`: 8 tests en verde;
  - `docker build --target test .` en local: código 0 (el paso de tests sale de la caché porque no cambió nada desde la última ejecución en verde);
  - en el PR #13, los tres checks en verde en `b522923` (run 41226191089), el último commit con código;
  - la búsqueda de claves, tokens, `secrets.` y rutas locales solo encuentra menciones en la documentación del propio change.

## Workflow follow-up

- `/opsx:verify` sin problemas críticos y `/opsx:archive` en esta misma rama. Después, el PR pasa de borrador a listo para revisión (D11). El merge lo hace el usuario con *squash*.
- Tras el merge:
  - comprobar que la CI se ejecuta en el push a `main`;
  - en las semanas siguientes, revisar los primeros PRs de Dependabot: si actualiza etiqueta y digest o solo el digest, si mantiene los SHA con su comentario y si respeta `Directory.Packages.props`. Anotar lo observado en el documento 09;
  - si 4.2 no pudo lanzarse en la rama, lanzar la medición desde `main` y completar el documento 07.
