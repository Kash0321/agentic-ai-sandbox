# Tasks

Rama de trabajo: `feat/add-container-build`. Referencias: `design.md` (decisiones D1 a D9 y resultados del explore) y `specs/`. Docker Desktop debe estar arrancado para los grupos 3 a 5. Al terminar: salir de Docker Desktop y ejecutar `wsl --shutdown`.

## 1. Saltos de línea normalizados

- [x] 1.1 Crear `.gitattributes` según D8. Verificar:
  - `git add --renormalize .` seguido de `git status` no muestra cambios, salvo el propio `.gitattributes`;
  - `git check-attr eol -- Dockerfile compose.yaml x.sh` devuelve `lf` para los tres;
  - un fichero temporal con CRLF añadido al índice aparece como `i/lf` en `git ls-files --eol`. Después, sacarlo del índice y borrarlo.
- [x] 1.2 Comprobar el escenario de extracción: en un clon temporal fuera del repo, con `core.autocrlf=true`, los ficheros `*.yaml` y `Dockerfile` (cuando exista; repetir en 5.1) salen con `w/lf` en `git ls-files --eol`.
- [x] 1.3 Documentación: sección de normalización de saltos de línea en `docs/learn/08-estructura-y-estandares-de-la-solucion-dotnet.md` (`text=auto`, `eol`, índice frente a árbol de trabajo, `core.autocrlf`, por qué `.editorconfig` no fija `end_of_line`), con lo observado en 1.1 y 1.2. Añadir los términos a `docs/learn/glosario.md`. Verificar que no hay enlaces relativos rotos ni flechas no monoespaciadas.

## 2. Globalización invariante

- [x] 2.1 Añadir `<InvariantGlobalization>true</InvariantGlobalization>` a `Directory.Build.props` (D6). Verificar con `dotnet msbuild <proyecto> -getProperty:InvariantGlobalization` que los siete proyectos devuelven `true`.
- [x] 2.2 Escribir `GlobalizationTests` en `tests/Sandbox.Scenarios.Tests`, con nombres `Metodo_Condicion_Resultado`:
  - `1234.5` con formato `N2` y la cultura actual da `1,234.50`;
  - pedir la cultura `es-ES` falla con `CultureNotFoundException`. Si el comportamiento observado es otro, ajustar la spec y el `design.md` con lo observado antes de seguir.

  Verificar con `dotnet test` en verde y, como prueba negativa temporal, que quitando la propiedad del 2.1 los dos tests fallan en esta máquina. Revertir y comprobar `git status`.
- [x] 2.3 Crear `docs/adr/0001-globalizacion-invariante.md` con el contexto, la decisión, las alternativas (B y C de D6) y las consecuencias para la fase 3.
- [x] 2.4 Documentación: sección de globalización invariante en el documento 08 (qué es ICU, qué hace `InvariantGlobalization`, qué es `PredefinedCulturesOnly`, por qué las imágenes *chiseled* lo activan y qué implica para el dominio), contrastada con Microsoft Learn y con la fecha de verificación. Incluir lo observado en 2.2, enlazar el ADR y añadir los términos al glosario.

## 3. Imagen: `.dockerignore` y `Dockerfile`

- [x] 3.1 Reverificar en la fuente primaria y anotar aquí la fecha:
  - la última versión estable del runtime y del SDK de .NET 10 (`releases.json`);
  - las etiquetas `sdk:<sdk>-noble` y `runtime:<runtime>-noble-chiseled` en MCR;
  - el digest del índice multiplataforma de cada una.

  Si han cambiado respecto al explore (10.0.403 / 10.0.14), usar las nuevas.

  **Reverificado el 2026-10-31 a las 16:25**, sin cambios:
  - `releases.json`: runtime 10.0.14 (2026-10-25), SDK 10.0.403;
  - `sdk:10.0.403-noble`: `sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b`;
  - `runtime:10.0.14-noble-chiseled`: `sha256:3eb54ac708855f74b032de48e02437d15fb083b2aa593d49fdd512bb17f3e944`.

  Los dos digests son de listas de manifiestos multiplataforma (`manifest.list.v2+json`).
- [x] 3.2 Crear `.dockerignore` de lista blanca (D5). Verificar en la salida `--progress=plain` del primer build (3.3) que el contexto transferido ocupa unos pocos kB y que la compilación no usa los `bin/` y `obj/` locales.
- [x] 3.3 Crear el `Dockerfile` con las etapas `restore`, `build`, `test`, `publish` y `final` (D1 a D4). Verificar que `docker build --target test .` termina en verde con los tests de los tres proyectos y sin advertencias de compilación.
- [x] 3.4 Verificar los escenarios negativos con cambios temporales, revertidos después y con `git status` limpio:
  - un test que falla hace fallar `--target test` y su nombre aparece en la salida;
  - un `.csproj` sin su `COPY` en `restore` hace fallar la restauración identificando el proyecto;
  - una etapa temporal con `RUN --network=none getent hosts api.nuget.org` falla.
- [x] 3.5 Verificar la caché: tras un build completo, modificar un `.cs`, reconstruir `--target test` y comprobar que el paso de restauración sale `CACHED`. Revertir el cambio.
- [x] 3.6 Verificar la imagen final (`docker build --target final -t agentic-sandbox-runner:local .`):
  - `docker image inspect` da un `User` distinto de 0 y el `ENTRYPOINT` esperado;
  - `docker run --rm` devuelve 0;
  - el listado de `/app` (con `docker create` + `docker export`) solo contiene la aplicación publicada;
  - su `runtimeconfig.json` tiene `System.Globalization.Invariant: true`.
- [x] 3.7 Verificar que no hay secretos:
  - crear en la raíz `secrets/canary` y `.env` con un valor conocido (ignorados por Git);
  - construir `--target test` y `final`;
  - comprobar que el valor no aparece en ninguna de las dos imágenes (`grep` en la de test; `docker export` + búsqueda en la final);
  - comprobar que `docker history --no-trunc` y las `Env` de la final solo contienen lo heredado de la base;
  - borrar los canarios.
- [x] 3.8 Documentación: actualizar el documento 07:
  - pasar de "diseño previsto" a lo implementado: etapas, lista blanca, `RUN --network=none`, digests y su actualización manual, imagen *chiseled* sin shell y cómo inspeccionarla, limpieza de disco con `docker image prune` y `docker builder prune`;
  - rellenar la tabla del experimento con las cifras de `design.md` y las de 3.3 y 3.5, marcadas como una sola medición en este equipo, con la conclusión según el criterio del documento;
  - revisar las marcas **(no verificado)** que estos resultados resuelven;
  - añadir los términos al glosario.

## 4. Compose: `runner` endurecido

- [x] 4.1 Crear `compose.yaml` con el servicio `runner` según D7. Verificar que `docker compose config --quiet` no da errores y que `docker compose run --rm runner` devuelve 0 y no deja contenedores (`docker ps -a`).
- [x] 4.2 Verificar el endurecimiento con un contenedor sin `--rm` (`docker compose run --name runner-check runner`): `docker inspect runner-check` muestra `ReadonlyRootfs: true`, `CapDrop: [ALL]`, `no-new-privileges`, límites de memoria y CPU, `NetworkMode: none` y ningún secreto montado. Eliminar el contenedor después.
- [x] 4.3 Documentación:
  - en el documento 07, la sección de compose con lo implementado y el aviso observado con un secreto cuyo fichero no existe (por qué la fase 2 debe validar la clave en la app);
  - en el `README.md`, la sección de contenedores con los comandos que funcionan hoy (`docker build --target test .`, `docker compose run --rm runner`), sin `--scenario` hasta la fase 4, y la estructura con `compose.yaml`.

  Verificar que los comandos del `README.md` funcionan tal como están escritos.

## 5. Integración y cierre

- [x] 5.1 Clon limpio: clonar la rama en un directorio temporal fuera del repo, con `core.autocrlf=true`, y ejecutar allí `dotnet build` (sin advertencias), `dotnet test` (en verde) y `docker build --target test .` (en verde). Comprobar con `git ls-files --eol` que `Dockerfile` y `compose.yaml` salen con LF (cierra el 1.2).
- [x] 5.2 Actualizar `docs/plan.md`:
  - el estado de `setup-dotnet-solution` ("fusionado") y de `add-container-build`;
  - el entregable `docker-compose.yml` pasa a `compose.yaml`.

  Actualizar `docs/learn/README.md`: fila de la fase 1 y backlog, por ejemplo escaneo de imágenes y SBOM para `add-ci-pipeline`.
- [x] 5.3 Validar y cerrar:
  - `openspec validate --all --strict` sin errores;
  - `dotnet build` sin advertencias y `dotnet test` en verde en el repo;
  - `docker build --target test .` en verde;
  - revisión final de que ningún fichero versionado contiene secretos ni rutas locales.

## Workflow follow-up

- `/opsx:verify` sin problemas críticos y `/opsx:archive` en esta misma rama, antes de abrir el PR (`AGENTS.md`, secciones 6 y 8).
- Commits y PR solo cuando el usuario lo pida; el merge lo hace el usuario con *squash*.
- Siguiente change de la fase 1: `add-ci-pipeline`, que construye este mismo `Dockerfile`, añade Dependabot (también para los digests de las imágenes) y hace la medición en otro equipo.
- Fuera de este change, en su propia rama `fix/`: el elemento de `operations.apply.guidance` de `openspec/config.yaml` que contiene `(sección 8): ` se interpreta como un mapa y la CLI descarta todas las indicaciones de `apply`. Hay que ponerlo entre comillas.
