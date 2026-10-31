# Design

## Context

Motivación y alcance en `proposal.md`; requisitos en `specs/container-build` y `specs/build-standards`.

Estado de partida observado:

- Solución .NET 10 con 4 proyectos de código y 3 de tests (`AgenticSandbox.slnx`), gestión centralizada de paquetes, `global.json` con SDK mínimo `10.0.100`, `rollForward: latestFeature` y `test.runner: Microsoft.Testing.Platform`. El único paquete es `xunit.v3`.
- `Sandbox.Runner` es una consola cuyo `Program.cs` hace `return 0;`.
- Git: el índice no tiene ningún fichero con CRLF (`git ls-files --eol`), y el árbol de trabajo de este equipo está en CRLF por `core.autocrlf=true`. No existe `.gitattributes`. `.editorconfig` no fija `end_of_line` a propósito.
- Equipo de desarrollo: Surface Go 2 (2 núcleos, 8 GB), Docker Desktop 4.97.0, motor y CLI 29.9.2, Compose v5.6.1, buildx 0.38.1, WSL2 limitado a 3 GB y 2 procesadores. SDK local 10.0.403.
- El diseño general (Dockerfile explícito, flujo híbrido, secretos por fichero) ya está decidido en `docs/learn/07-contenedores-en-sistemas-agenticos.md` y en `AGENTS.md`, sección 10.

### Explore previo: experimentos y resultados

Antes de proponer se hicieron experimentos el 2026-10-31, fuera del repo, en el scratchpad de la sesión. El contexto de build era una copia del repo sin `.git`, con sus `bin/` y `obj/` reales y con ficheros canario en `secrets/deepseek_api_key` y `.env`. Todas las cifras son **una sola medición** en este equipo.

**Consultas a fuentes primarias (2026-10-31):**

| Qué | Resultado | Fuente |
|---|---|---|
| Última versión de .NET 10 | runtime 10.0.14 (2026-10-25), SDK 10.0.403 | `releases.json` de .NET 10.0 |
| A qué apunta `10.0` | Mismo digest que `10.0-noble`, en `sdk` y en `runtime` | Digests del registro de MCR |
| `resolute` | Existe (runtime desde 10.0.6, SDK desde 10.0.202), pero no es el valor por defecto | Lista de etiquetas de MCR |
| Tamaño comprimido del runtime (amd64) | `noble` 81 MB, `noble-chiseled` 42 MB, `noble-chiseled-extra` 57 MB, `alpine` 41 MB, `resolute-chiseled` 43 MB | Manifiestos de MCR |
| Tamaño comprimido del SDK (amd64) | `noble` 342 MB, `alpine` 301 MB | Manifiestos de MCR |
| Configuración del runtime *chiseled* | `User=1654`; define `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` | Configuración de la imagen |
| Configuración de *chiseled-extra* | `User=1654`; **no** define esa variable (incluye ICU y tzdata) | Configuración de la imagen; Microsoft Learn, *.NET container images* |
| Nombre del fichero de compose | `compose.yaml` es el nombre preferido; `docker-compose.yml` se admite por compatibilidad | docs.docker.com, *How Compose works* |
| `RUN --network=none` fuera de Docker | Documentado en el manual de Containerfile para Buildah y Podman | `Containerfile(5)` |

**Experimentos con el motor:**

| Experimento | Observado |
|---|---|
| `.dockerignore` de lista blanca | Contexto de **9,47 kB**; los `bin/` y `obj/` locales no se enviaron |
| `RUN --network=none` | `getent hosts api.nuget.org` falla dentro del paso; los **6 tests pasan** |
| Caché del `restore` tras cambiar un `.cs` | Paso de restauración `CACHED`; se repiten solo el build y los tests |
| Propiedad `InvariantGlobalization=true` | El `runtimeconfig.json` publicado tiene `System.Globalization.Invariant: true` **y** `System.Globalization.PredefinedCulturesOnly: true` |
| Canarios | Ni en la imagen final (322 ficheros exportados con `docker export`) ni en la de test (`grep` y `find`) |
| Historial de la imagen final | Las únicas `ENV` son las de la imagen base |
| Imagen final | `User=1654`, punto de entrada `dotnet Sandbox.Runner.dll`, 141 MB según `docker image ls` (la misma cifra que muestra la base de runtime); contiene solo los ensamblados publicados, sus `.pdb`, `deps.json`, `runtimeconfig.json` y el *apphost* |
| Ejecución endurecida (CLI y compose) | `read_only`, `cap_drop: ALL`, `no-new-privileges`, sin red y `mem_limit: 256m`: código de salida 0 y sin avisos por `/tmp` |
| Secreto de compose con fichero inexistente | **Solo un aviso** (`secret file ... does not exist`); el contenedor arranca y termina con 0. Contradice lo que se suponía en el explore |
| Descargar por `etiqueta@digest` | La imagen queda sin etiqueta local (`<none>`): con digest, la etiqueta es solo informativa |

**Tiempos y memoria:**

| Escenario | Tiempo | Pico de `vmmemWSL` | Mínimo de RAM libre en el equipo |
|---|---|---|---|
| Descarga de las dos imágenes base | 87,5 s | 2,1 GB | 865 MB |
| `--target test` en frío (con las bases ya descargadas) | 95,9 s (extracción del SDK ~31 s, restore 17,5 s, build 24,8 s, tests 7,2 s) | 2,5 GB | 517 MB |
| `--target test` sin cambios | 4,3 s (todo desde caché) | 2,4 GB | 855 MB |
| `--target test` tras cambiar un `.cs` | 44,0 s | 2,5 GB | 703 MB |
| `dotnet test` nativo tras cambiar un `.cs` (Docker arrancado) | 35,6 s | 2,3 GB | 285 MB |
| `dotnet test` nativo sin cambios (Docker arrancado) | 29,9 s | 2,3 GB | 336 MB |

Lectura: tras un cambio de código, el contenedor tarda ×1,24 lo que tarda el nativo, y el nativo estaba penalizado por la memoria que ocupaba Docker. Los 4,3 s del caso "sin cambios" no se pueden comparar con el nativo: Docker no ejecuta los tests si nada cambió, mientras que `dotnet test` siempre los ejecuta.

**Suposiciones sin verificar al proponer** (se comprueban en las tareas; resultado del apply, 2026-10-31):

- Que pedir `CultureInfo` `es-ES` con `PredefinedCulturesOnly` lance `CultureNotFoundException`. **Confirmado** en la tarea 2.2, en Windows y en el contenedor Linux.
- Que el escenario "olvidar un `.csproj` en el `restore`" falle con un mensaje que identifique el proyecto. **Confirmado** en la tarea 3.4: `MSB3202` con la ruta del proyecto que falta.
- Que un test en rojo haga fallar `docker build --target test` con el nombre del test en la salida. **Confirmado** en la tarea 3.4.
- El comportamiento con Podman. **Sigue sin verificar**: Podman no está instalado.

## Goals / Non-Goals

**Goals:**

- Que `docker build --target test .` sea el comando de verificación en contenedor, rápido en caliente y sin red en la ejecución de tests.
- Una imagen final mínima, no root y sin secretos, y una ejecución local endurecida por defecto.
- El mismo comportamiento de globalización en local, en los tests y en la imagen.
- Que todo funcione con Docker y Podman sin funciones exclusivas de uno solo (por diseño; solo se prueba Docker).

**Non-Goals:**

- Optimizar el tamaño de la imagen más allá de elegir la base (sin *trimming*, AOT ni *self-contained*).
- Caché de NuGet con `RUN --mount=type=cache`: añade complejidad y la caché de capas ya cubre el caso habitual.
- Exportar resultados de tests (TRX) desde la imagen: lo necesitará la CI (`add-ci-pipeline`).

## Decisions

### D1. Imágenes base: SDK `10.0.403-noble` y runtime `10.0.14-noble-chiseled`, fijadas por etiqueta y digest

- **SDK `noble`**: es el valor por defecto de `10.0` y coincide con el SDK local (10.0.403). Las imágenes SDK no tienen variante *chiseled* (Microsoft Learn).
- **Runtime `noble-chiseled`**: 42 MB comprimida, sin shell ni gestor de paquetes, no root de serie. Comparte distribución (glibc) con la imagen SDK, así que no hay sorpresas de RID entre compilar y ejecutar.
- **Se fija el digest del índice multiplataforma**, no el del manifiesto amd64, para no romper la construcción en arm64 aunque no se pruebe. La etiqueta se mantiene delante del digest por legibilidad; manda el digest.
- Las versiones y los digests se **reverifican al implementar** contra MCR y `releases.json`, y se anota la fecha. Mientras no haya Dependabot (`add-ci-pipeline`), su actualización es manual.

Alternativas descartadas:

| Opción | Por qué no |
|---|---|
| `alpine` | musl en lugar de glibc; la ventaja de tamaño frente a *chiseled* es mínima (41 frente a 42 MB) |
| `resolute` | No es el valor por defecto de Microsoft para `10.0`; no aporta nada al proyecto hoy |
| `noble` sin *chiseled* | El doble de tamaño y con shell; más superficie de ataque |
| `noble-chiseled-extra` | Solo hace falta si se necesitan ICU o tzdata; ver D6 |
| Solo etiqueta, sin digest | Una etiqueta como `10.0.14-noble-chiseled` puede reconstruirse con parches del sistema operativo; sin digest, el build no es reproducible |

### D2. Restauración cacheable con un `COPY` explícito por proyecto

Etapa `restore`:
- copia `global.json`, `Directory.Build.props`, `Directory.Packages.props` y `AgenticSandbox.slnx`;
- copia cada `.csproj` en su ruta, con una línea por proyecto;
- ejecuta `dotnet restore AgenticSandbox.slnx`.

`Directory.Build.props` es necesario aquí porque fija el `TargetFramework`. `.editorconfig` se copia en la etapa `build`, porque solo afecta a la compilación (`EnforceCodeStyleInBuild`) y así no invalida la restauración.

Restaurar la solución, y no proyecto a proyecto, convierte el olvido de un `.csproj` en un error explícito (escenario de la spec). La auditoría de vulnerabilidades de NuGet ocurre en la restauración, que es la única etapa con red.

Alternativas: `COPY --parents` (cómodo, pero es una extensión de BuildKit; no se ha comprobado en Podman), y copiar todo antes del `restore` (pierde la caché, que es justo el error típico que describe el documento 07).

### D3. Etapa `test` sin red, compilando una sola vez en `Release`

- `build`: `dotnet build AgenticSandbox.slnx --no-restore -c Release`.
- `test`: `RUN --network=none dotnet test --solution AgenticSandbox.slnx --no-build -c Release`.
- `publish`: parte de `build` con `dotnet publish src/Sandbox.Runner/Sandbox.Runner.csproj --no-build -c Release -o /app`.

Se compila una vez y se reutiliza en `test` y `publish`: lo que se prueba es exactamente lo que se publica. `RUN --network=none` está documentado tanto en Docker como en `Containerfile(5)`, para Buildah y Podman.

Alternativas: `docker build --network=none` a nivel de CLI (dejaría sin red también la restauración), o no aislar la red (no demostraría el requisito "sin red" de `build-standards`).

Consecuencia: en contenedor los tests se ejecutan en `Release` y en local en `Debug`. Se acepta, porque hoy no hay código cuyo comportamiento dependa de la configuración.

### D4. Imagen final

- `FROM` del runtime *chiseled* fijado, `WORKDIR /app` y `COPY --from=publish /app ./`.
- `ENTRYPOINT ["dotnet", "Sandbox.Runner.dll"]` en forma *exec*: el proceso es el PID 1 y recibe las señales directamente.
- No hace falta `USER`: la base ya es no root (`1654`). La spec lo verifica inspeccionando la imagen, así que un cambio de base que lo rompiera se detectaría.
- **Se conservan los `.pdb`** (decisión del responsable): las trazas de excepción muestran fichero y línea, y el coste en tamaño es despreciable.

### D5. `.dockerignore` de lista blanca

Excluye todo (`*`) y vuelve a incluir los ficheros raíz necesarios, `src/` y `tests/`. Después vuelve a excluir `**/bin/`, `**/obj/` y `**/TestResults/`. Un fichero nuevo en la raíz (un `.env`, una carpeta de secretos con otro nombre) queda fuera por defecto. El experimento dio un contexto de 9,47 kB.

Alternativa: lista negra (`bin/`, `obj/`, `.git`, `secrets/`, `.env`...). Es más habitual, pero falla de forma insegura: lo que no se haya previsto entra en el contexto.

### D6. Globalización invariante en `Directory.Build.props`

Decisión del responsable tras el explore: `<InvariantGlobalization>true</InvariantGlobalization>` en `Directory.Build.props`, que aplica a todos los proyectos, incluidos los de tests.

Motivo: la imagen *chiseled* ya corre en modo invariante (variable de entorno de la base), mientras que la imagen SDK de los tests y Windows en local usan ICU. Con la propiedad, los tres entornos se comportan igual y el comportamiento queda declarado en el código, no solo en la imagen. Encaja con el principio de determinismo de `AGENTS.md`.

| Opción | Pros | Contras |
|---|---|---|
| **A. Invariante en toda la solución** (elegida) | Mismo comportamiento en local, en los tests y en la imagen; imagen de 42 MB | No hay formato por cultura; `PredefinedCulturesOnly` hace que pedir `es-ES` falle |
| B. `noble-chiseled-extra` | No toca el build; mismo comportamiento que la SDK de los tests | 57 MB; Windows sigue siendo distinto; el comportamiento depende de los datos ICU de cada entorno |
| C. `noble` sin *chiseled* | Shell para depurar | 81 MB y más superficie |

**Trasciende este change**: condiciona el dominio de la fase 3 y cualquier salida formateada. Se registra como ADR en `docs/adr/`.

### D7. Compose: `compose.yaml` con un `runner` endurecido, sin red ni secretos

- **Nombre `compose.yaml`**, el canónico según Docker. El `README.md` y el plan mencionaban `docker-compose.yml` y se corrigen.
- **Servicio `runner`**: construye la etapa `final` del `Dockerfile` y etiqueta la imagen con un nombre local estable. Se usa con `docker compose run --build --rm runner`: en el apply se observó que, sin `--build`, compose reutiliza la imagen ya construida aunque el código haya cambiado. `docker compose run --rm runner` sigue cumpliendo los escenarios de la spec, pero puede ejecutar una imagen desfasada.
- **Endurecimiento** como atributos del servicio: `read_only: true`, `cap_drop: [ALL]`, `security_opt: ["no-new-privileges:true"]`, `mem_limit: 256m`, `cpus: 1.0` y `network_mode: none`. Todos se probaron en el experimento.
  - Se usan `mem_limit`/`cpus` en lugar de `deploy.resources` porque son los que se probaron con `compose run`.
  - Los valores son de partida: el Runner actual no hace nada, y la fase 2 los revisará con carga real.
- **Sin secretos declarados**: el experimento mostró que un secreto cuyo fichero falta solo da un aviso. Declararlo ahora daría una falsa sensación de control; la fase 2 lo declara y hace que la app valide la clave.
- **Sin `tmpfs`**: con el Runner actual no hizo falta escribir en disco. Si la fase 2 lo necesita (por ejemplo, para el socket de diagnóstico de .NET o para ficheros temporales del cliente HTTP), se añade allí con el caso que lo justifique.

### D8. `.gitattributes`

```
* text=auto
Dockerfile text eol=lf
*.sh text eol=lf
*.yml text eol=lf
*.yaml text eol=lf
```

- `text=auto` asegura LF en el índice para cualquier fichero de texto, aunque alguien no tenga `core.autocrlf`.
- `eol=lf` fuerza LF en el árbol de trabajo para lo que se interpreta en Linux.
- El resto de ficheros conserva la conversión de cada desarrollador, coherente con no fijar `end_of_line` en `.editorconfig`.
- Como el índice ya está todo en LF, se espera que `git add --renormalize .` no produzca cambios. Se comprueba en las tareas.

### D9. Cómo se verifican los requisitos

- Los requisitos de `container-build` se verifican con comandos (`docker build`, `docker image inspect`, `docker export`, `docker compose run`, `docker inspect`), con escenarios negativos temporales que se revierten. La CI los automatizará en `add-ci-pipeline`.
- La globalización invariante se cubre con un test xUnit en `Sandbox.Scenarios.Tests`: formato `N2` con la cultura actual, y que `es-ES` no está disponible. También con la lectura de `InvariantGlobalization` de cada proyecto (`dotnet msbuild -getProperty`) y del `runtimeconfig.json` publicado.
- No se añaden tests xUnit que lean el `Dockerfile`: dentro de la etapa `test` el `Dockerfile` no está en el contexto (lista blanca), y es la CI la que debe comprobar la definición de la imagen.

## Risks / Trade-offs

- **[Memoria del equipo]** Durante el build quedan entre 285 y 517 MB libres. → Arrancar Docker solo para verificar, cerrar aplicaciones pesadas y ejecutar `wsl --shutdown` al terminar, como dice el documento 07.
- **[Digests que envejecen]** Fijar por digest congela también los parches de seguridad de la base. → Actualización manual en cada change que toque el `Dockerfile` hasta que exista Dependabot (`add-ci-pipeline`).
- **[Lista de `.csproj` duplicada]** Cada proyecto nuevo exige una línea en el `Dockerfile`. → El olvido falla de forma explícita (escenario de la spec), y se documenta en el documento 07.
- **[`Release` en contenedor y `Debug` en local]** Pueden aparecer diferencias de comportamiento. → Se acepta hoy. Si aparecen, el contenedor manda (`AGENTS.md`, sección 10).
- **[Modo invariante]** Si una fase futura necesita formato por cultura, habrá que revisar D6 con un change propio y actualizar el ADR. → Las salidas que vea el LLM o el usuario se formatean de forma explícita.
- **[Sin shell en la imagen final]** Depurar dentro es más difícil. → Inspeccionar con `docker export` o con la imagen de la etapa `build` (`--target build`), que sí tiene shell.
- **[Disco]** La imagen de la etapa `test` ocupa ~1,4 GB en local. → Documentar `docker image prune` y `docker builder prune` en el documento 07.
- **[Podman sin probar]** → Se evitan funciones exclusivas; queda anotado como no verificado.

## Migration Plan

No hay nada que migrar. Desde que se fusione este change, la definición de terminado (sección 8 de `AGENTS.md`) exige `docker build --target test` en verde. Para revertir, basta con eliminar los ficheros nuevos y la propiedad de `Directory.Build.props`.

## Open Questions

- ¿Necesitará la fase 2 un `tmpfs` en `/tmp` con el sistema de ficheros de solo lectura cuando haya un cliente HTTP real? Se resuelve allí, con el caso real.
