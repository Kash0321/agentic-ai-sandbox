# 7. Contenedores en sistemas agénticos

> **Estado**: actualizado con el change `add-container-build` (fase 1). La imagen, la etapa de tests y el servicio `runner` ya existen, y este documento describe lo implementado y lo medido. La CI está implementada con `add-ci-pipeline` (detalle en el documento 9), y la medición en un runner de GitHub se hizo tras su merge. La gestión real del secreto llega en la fase 2. **Verificado**: 2026-10-31 contra Microsoft Learn, la documentación de Docker y el registro de MCR lo que se cita con enlace, y con pruebas reales en este repositorio; lo demás se marca **(no verificado)**.

## Por qué contenerizar este proyecto

| Motivo | Qué aporta aquí |
|--------|-----------------|
| **Reproducibilidad** | Cualquiera construye y ejecuta lo mismo sin instalar SDK ni dependencias a mano |
| **Paridad con la CI** | La CI construye el mismo `Dockerfile`: "en mi máquina funciona" deja de ser un argumento |
| **Aislamiento** | La app corre con un sistema de ficheros, usuario y red acotados |
| **Límite de impacto** | Si algo falla o es manipulado, el daño queda dentro del contenedor |
| **Aprendizaje** | Es una competencia útil y se practica en un caso sencillo y controlado |

Es un caso fácil de contenerizar: una app de consola, sin base de datos, con una única dependencia externa (la API de DeepSeek). Los puntos delicados son tres: **la API key**, que **el repositorio es público** y que **la consola es interactiva**.

## Conceptos base

- **Imagen**: plantilla inmutable con el sistema de ficheros y la configuración de arranque. Se compone de **capas**.
- **Contenedor**: ejecución de una imagen, con su propio proceso, red y sistema de ficheros.
- **`Dockerfile`**: receta para construir una imagen. Cada instrucción genera una capa.
- **Build multi-etapa**: varias etapas en un mismo `Dockerfile`; solo la última forma la imagen final. Permite compilar con el SDK (pesado) y ejecutar con el runtime (ligero). Microsoft lo recomienda para reducir el tamaño de la imagen.
- **Caché de capas**: si una capa y todo lo anterior no cambian, se reutiliza. Por eso se copian primero los ficheros que definen dependencias (`*.csproj`, `Directory.Packages.props`) y se restauran **antes** de copiar el código.
- **Digest**: identificador inmutable de una imagen (`@sha256:...`). Fijar la imagen base por tag y, mejor, por digest evita que el contenido cambie sin avisar. Microsoft lo recomienda como buena práctica.
- **Compose**: define servicios, redes, volúmenes y secretos de una aplicación multicontenedor en un fichero.
- **Bind mount y volumen**: forma de compartir ficheros con el contenedor. El primero apunta a una carpeta del anfitrión; el segundo lo gestiona el motor de contenedores.

## Cómo se contenerizan las apps .NET

Dos vías, ambas oficiales:

| Vía | Cómo | Cuándo |
|-----|------|--------|
| **`Dockerfile`** | `docker build` con un fichero propio | Máximo control; permite una etapa `test`; enseña cada pieza |
| **Contenedores del SDK** | `dotnet publish /t:PublishContainer`, **sin Dockerfile** | Casos simples; el SDK genera la imagen |

Datos verificados sobre la vía del SDK:

- Está incluida desde el SDK 8.0.200 y no necesita paquetes adicionales. En aplicaciones de consola puede hacer falta activar `EnableSdkContainerSupport`.
- **No necesita Docker para construir** la imagen; Docker o Podman solo hacen falta para ejecutarla en local.
- La imagen base se infiere del proyecto (`mcr.microsoft.com/dotnet/runtime` para una consola) y se puede cambiar con `ContainerBaseImage` o `ContainerFamily`.
- Con .NET 8 o superior y las imágenes de Microsoft, el contenedor corre por defecto con el usuario **sin privilegios** `app`.

Las imágenes *chiseled* de Ubuntu llevan solo lo imprescindible para .NET: **no tienen shell ni gestor de paquetes**, tienen menos CVE y ya vienen con usuario no root.

**Decisión del proyecto (criterio propio)**: usar `Dockerfile` explícito, porque el objetivo es aprender y necesitamos la etapa `test`. La vía del SDK queda como comparación posible.

## Cómo está implementado

Los ficheros son [`Dockerfile`](../../Dockerfile), [`.dockerignore`](../../.dockerignore) y [`compose.yaml`](../../compose.yaml), en la raíz. Las decisiones y su porqué están en el `design.md` del change `add-container-build` (archivado en `openspec/changes/archive/`) y en la spec `container-build` (`openspec/specs/container-build/spec.md`).

### Etapas del `Dockerfile`

```
 sdk:10.0.403-noble@sha256:...
 +-----------+     +---------+     +----------+
 |  restore  | --> |  build  | --> |   test   |  <-- docker build --target test .
 | (con red) |     |         |     | (sin red)|
 +-----------+     +---------+     +----------+
                        |
                        v
                   +---------+
                   | publish |
                   +---------+
                        |  COPY /app
                        v
 runtime:10.0.14-noble-chiseled@sha256:...
 +--------------------------------------------+
 | final: /app, usuario 1654,                 |
 | ENTRYPOINT ["dotnet", "Sandbox.Runner.dll"]|
 +--------------------------------------------+
```

- **`restore`**: copia `global.json`, `Directory.Build.props`, `Directory.Packages.props`, el `.slnx` y **cada `.csproj` en su ruta, con una línea por proyecto**, y restaura la solución. Es la única etapa con red, y también donde NuGet audita vulnerabilidades.
- **`build`**: copia `.editorconfig` (solo afecta a la compilación, así que no invalida la restauración), `src/` y `tests/`, y compila una vez en `Release`.
- **`test`**: `RUN --network=none dotnet test --no-build`. Es lo que ejecuta `docker build --target test .` y, después, la CI.
- **`publish`**: publica el Runner reutilizando lo compilado (`--no-build`): lo que se prueba es exactamente lo que se publica.
- **`final`**: runtime *chiseled*, `/app` con lo publicado y `ENTRYPOINT ["dotnet", "Sandbox.Runner.dll"]` en forma *exec* (el proceso es el PID 1 y recibe las señales).

### Por qué un `COPY` por proyecto

`COPY` con comodines no conserva las carpetas, así que para que `restore` sea una capa cacheable hay que copiar cada `.csproj` a su ruta. Cuesta una línea por proyecto nuevo, pero **olvidarla falla de forma explícita**: al restaurar la solución, `dotnet restore` se queja de que no encuentra el proyecto (`error MSB3202: The project file ".../Sandbox.Agents.Tests.csproj" was not found`, observado). BuildKit tiene `COPY --parents`, que conserva la estructura, pero es una extensión que no se ha comprobado en Podman.

### Contexto de build: `.dockerignore` de lista blanca

El **contexto** es lo que el cliente envía al motor antes de construir. El `.dockerignore` del proyecto **excluye todo** (`*`) y vuelve a incluir solo lo necesario (`!src/`, `!tests/`, los ficheros raíz de build). Después excluye otra vez `**/bin/`, `**/obj/` y `**/TestResults/`. Un fichero nuevo en la raíz, sea un `.env` o una carpeta de secretos con otro nombre, queda fuera **por defecto**. La alternativa habitual, una lista negra (`bin/`, `.git`, `.env`...), falla de forma insegura: lo que no se previó, entra.

Observado: con los `bin/` y `obj/` reales del repo presentes, el contexto ocupó **10,23 kB**.

### Tests sin red: `RUN --network=none`

`--network=none` en una instrucción `RUN` deja ese paso sin red. Está documentado tanto en Docker como en el manual de `Containerfile` de Buildah y Podman ([Containerfile(5)](https://man.archlinux.org/man/Containerfile.5.en)). Es más fino que `docker build --network=none`, que dejaría sin red también la restauración.

Observado: en un paso con `--network=none`, `getent hosts api.nuget.org` falla con código 2; en la misma imagen con red, resuelve. Los tests pasan sin red.

### Imágenes base fijadas por etiqueta y digest

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:de4fe90a...
```

- **Se fija el digest de la lista de manifiestos multiplataforma** (no el del manifiesto `amd64`), para que el mismo `Dockerfile` sirva en otras arquitecturas.
- **Manda el digest**: al descargar por `etiqueta@digest`, la imagen queda en local como `mcr.microsoft.com/dotnet/sdk:<none>`. La etiqueta solo informa a quien lee el `Dockerfile` (observado).
- **La etiqueta `10.0` apunta a `noble`** (Ubuntu 24.04): mismo digest que `10.0-noble` en MCR el 2026-10-31. Existe también `resolute`, que no es el valor por defecto.
- **Actualizar**: desde `add-ci-pipeline`, Dependabot (ecosistema `docker`) revisa estas imágenes cada semana y abre un PR. Todavía no se sabe si actualiza la etiqueta y el digest o solo el digest dentro de la misma etiqueta **(no verificado; pendiente de sus primeros PRs)**, así que el procedimiento manual sigue valiendo: consultar la última versión en `releases.json` de .NET, obtener el digest del registro de MCR y cambiar etiqueta y digest a la vez. Detalle en el [documento 9](09-integracion-continua-y-cadena-de-suministro.md).

### Imagen final *chiseled*

| Variante del runtime (amd64, comprimida, 2026-10-31) | Tamaño |
|---|---|
| `noble` | 81 MB |
| `noble-chiseled` (**la elegida**) | 42 MB |
| `noble-chiseled-extra` (con ICU y tzdata) | 57 MB |
| `alpine` | 41 MB |

- **Usuario no root**: la imagen base ya trae `User=1654` (`APP_UID`), así que no hace falta `USER` en el `Dockerfile`.
- **Globalización**: la base *chiseled* define `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`. Para que local, tests e imagen se comporten igual, el proyecto activa `InvariantGlobalization` en toda la solución ([ADR 0001](../adr/0001-globalizacion-invariante.md); detalle en el documento 8).
- **Sin shell**: no se puede hacer `docker run -it ... sh`. Para inspeccionarla:
  - `docker image inspect` (usuario, entorno, punto de entrada) y `docker history --no-trunc` (instrucciones);
  - `docker create` + `docker export` para listar o extraer su sistema de ficheros como un `tar`;
  - la imagen de la etapa `build` (`docker build --target build`), que sí tiene shell y el mismo código.
- **Contenido observado**: 322 ficheros. En `/app`, solo los ensamblados publicados, sus `.pdb`, `deps.json`, `runtimeconfig.json` y el *apphost*. Ningún fuente ni test, y sin SDK.

### Disco: qué ocupa y cómo se limpia

La imagen de la etapa `test` lleva el SDK y ronda **1,4 GB** en local; la final, la base de runtime más unos pocos KB. Para recuperar espacio ([documentación de Docker](https://docs.docker.com/engine/manage-resources/pruning/)):

- `docker image prune` borra las imágenes **colgantes** (sin repositorio ni etiqueta y sin contenedor que las use), como la imagen `test` anterior cuando se reconstruye. Las bases descargadas por digest no son colgantes: conservan el nombre del repositorio (observado con `docker image ls --filter dangling=true`).
- `docker builder prune` borra la caché de BuildKit que no se está usando. El siguiente build será "en frío".
- `docker system df` muestra cuánto ocupa cada cosa antes de borrar.

### Compose: el servicio `runner`

El fichero se llama `compose.yaml`, el nombre canónico según la documentación de Docker; `docker-compose.yml` se admite solo por compatibilidad. Hoy define un único servicio:

```bash
docker compose run --build --rm runner   # construye la etapa final si hace falta, ejecuta y borra el contenedor
```

- **One-shot**: `run --rm` ejecuta el Runner una vez, devuelve su código de salida y no deja el contenedor creado.
- **`--build`**: sin él, compose reutiliza la imagen `agentic-sandbox-runner:local` si ya existe, aunque el código haya cambiado. Con él, reconstruye antes de ejecutar (y gracias a la caché, si nada cambió, tarda poco).
- **`--scenario`** llegará con los escenarios de la fase 4; hoy el Runner no recibe argumentos y devuelve 0.
- **Panel de trazas** (perfil opcional `dashboard`, Aspire Dashboard autónomo): fase 5. Microsoft aclara que es una herramienta de desarrollo, no de monitorización en producción, y que por defecto exige un token de acceso.

### Endurecimiento del `runner`

Todo está declarado en `compose.yaml` y comprobado con `docker inspect` sobre un contenedor real:

| Opción en `compose.yaml` | Qué limita | Comprobado en `docker inspect` |
|---|---|---|
| (imagen base) usuario `1654` | El proceso no es root | `User=1654` |
| `read_only: true` | No se puede escribir en el sistema de ficheros raíz | `ReadonlyRootfs=true` |
| `cap_drop: [ALL]` | Sin capacidades del kernel (ni las que Docker da por defecto) | `CapDrop=["ALL"]` |
| `security_opt: [no-new-privileges:true]` | Ningún proceso puede ganar privilegios (por ejemplo, con binarios *setuid*) | `SecurityOpt=["no-new-privileges:true"]` |
| `mem_limit: 256m`, `cpus: 1.0` | Un fallo o un bucle no se come el equipo | `Memory=268435456`, `NanoCpus=1000000000` |
| `network_mode: none` | Sin red, solo la interfaz local | `NetworkMode=none` |

El Runner actual funciona con todas estas restricciones (código 0, sin avisos de escritura). Los límites de memoria y CPU son valores de partida: la fase 2 los revisará con carga real. La red se abrirá en la fase 2, solo para llegar a la API del modelo, y ese cambio quedará visible en su propio change.

La CI comprueba lo mismo en cada PR: el job `image` ejecuta la imagen final una vez con estas restricciones (`--read-only`, `--cap-drop ALL`, `no-new-privileges`, sin red, 256 MB y 1 CPU) y después la **escanea con Grype**. Política y detalle en el [ADR 0002](../adr/0002-checks-obligatorios-y-politica-de-vulnerabilidades.md) y en el [documento 9](09-integracion-continua-y-cadena-de-suministro.md).

### Secretos

El repositorio es público, así que:

- La API key **nunca** va en la imagen, el `Dockerfile` (`ARG`/`ENV`), `compose` ni un `.env` versionado. Solo se versiona `.env.example`.
- Opción recomendada: secreto de compose basado en **fichero** (`secrets/deepseek_api_key`, ignorado por Git), montado en `/run/secrets/` y leído con un proveedor de configuración por ficheros. A diferencia de una variable de entorno, no aparece en `docker inspect`. **(No verificado)** en esta sesión: la ruta de montaje y el proveedor concreto (fase 2).
- Alternativa simple: la variable de entorno `DeepSeek__ApiKey`.

**Lo observado en la fase 1 sobre los secretos de compose**: si un servicio declara un secreto cuyo fichero **no existe**, `docker compose run` solo muestra un aviso (`secret file ... does not exist`), y el contenedor arranca y termina con 0. Compose **no** protege contra una clave ausente. Por eso:

- en la fase 1 el `runner` no declara ningún secreto: declararlo sin usarlo daría una falsa sensación de control;
- en la fase 2, la propia aplicación debe fallar con un error claro si no encuentra la clave.

**Comprobado en la fase 1**: con un fichero canario en `secrets/` y un `.env` con valores conocidos en la raíz del repo, ninguno de los dos llegó a la imagen de test ni a la final, ni por nombre ni por contenido. Además, el historial y las variables de entorno de la imagen final solo contienen lo que hereda de la imagen base.

### CI

Implementada con `add-ci-pipeline` ([`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)). En cada PR y en cada push a `main` hay tres jobs, que son checks obligatorios del ruleset de `main`:

- `test`: `docker build --target test`, el mismo comando que en local;
- `image`: imagen final, ejecución endurecida y escaneo;
- `openspec`: validación estricta de las specs.

Token de solo lectura, acciones fijadas por SHA (y exigido por el repositorio), sin secretos y con caché de capas de BuildKit en GitHub Actions.

En el PR #13, `test` tardó 51 s en frío y 14 s con caché sin cambios de código. Es mucho menos que en la Surface Go 2, porque el runner no tiene sus límites de memoria. Todo el detalle está en el [documento 9](09-integracion-continua-y-cadena-de-suministro.md).

## Velocidad: ¿merece la pena "solo contenedor"?

Hay dos formas de entender "solo contenedor", con costes muy distintos:

| Modo | Coste | Comentario |
|------|-------|-----------|
| **Reconstruir la imagen en cada ciclo** | Alto y evitable | La caché de capas acelera el restore, pero la compilación se repite salvo que se configuren cachés de build |
| **Dev container** (desarrollar dentro de un contenedor persistente) | Bajo, con coste de montaje | Se parece más al uso nativo; exige configuración |

Factores a tener en cuenta:

- **CPU**: en contenedores Linux sobre WSL2 es cercana a la nativa **(no verificado)**.
- **I/O de ficheros**: es el coste real. Con el código en una carpeta de Windows montada en el contenedor, restaurar y compilar muchos ficheros pequeños puede ser mucho más lento. Se evita guardando el repo en el sistema de ficheros de WSL o usando volúmenes con nombre.
- **Herramientas**: depurador, IDE, `openspec`, `gh` y el asistente dentro o fuera del contenedor.
- **Costes fijos**: habilitar WSL2, instalar el motor (reinicio y permisos de administrador), RAM de la máquina virtual e imágenes que descargar.
- **Beneficio**: reproducibilidad. En un proyecto en solitario con un solo SDK, la CI sobre el mismo `Dockerfile` ya la proporciona casi toda.

**Decisión del proyecto**: flujo **híbrido**. `dotnet` local para iterar rápido y el contenedor como referencia para dar algo por terminado y para la CI. El flujo "solo contenedor" es un **experimento acotado en la fase 1**.

### Equipo de desarrollo actual y sus límites

Medido el 2026-10-31, **una sola vez** y con Docker Desktop recién arrancado y en reposo (sin construir nada). Son datos de este equipo, no una regla general.

| Dato | Valor |
|------|-------|
| Equipo | Surface Go 2, Intel Core m3-8100Y (2 núcleos, 4 hilos, 1,1 GHz base, sin ventilador) |
| RAM | 8 GB (unos 8.062 MB visibles) |
| Disco | SSD de 254 GB, unos 159 GB libres |
| Sistema | Windows 11 Business (edición Professional), build 26200 |
| Software instalado | Docker Desktop 4.97.0, motor 29.9.2, Compose v5.6.1, WSL2 |
| RAM libre antes de instalar | ~1,4 GB |
| RAM libre con Docker en reposo | ~0,5 GB |
| Coste de Docker en reposo | ~2,8 GB (`vmmemWSL` ~2,1 GB; Docker Desktop y sus procesos ~0,7 GB) |

Configuración de WSL2 aplicada (`%UserProfile%\.wslconfig`), basada en los parámetros documentados por Microsoft:

```ini
[wsl2]
memory=3GB
processors=2
swap=4GB

[experimental]
autoMemoryReclaim=gradual
sparseVhd=true
```

Por defecto, WSL2 reservaría el 50 % de la RAM (unos 4 GB aquí) y todos los procesadores lógicos, según la documentación de Microsoft.

Consecuencias para el flujo de trabajo:

- **Docker no se deja arrancado.** Al terminar se sale de Docker Desktop y se ejecuta `wsl --shutdown`, que devuelve la memoria. El arranque automático de Docker Desktop debe estar desactivado en sus ajustes.
- **Desarrollo diario nativo con `dotnet`.** Docker se usa para construir la imagen y verificar antes de dar algo por terminado.
- **La CI es la referencia real** sobre el mismo `Dockerfile`: los runners de GitHub no gastan RAM local.
- **El experimento de velocidad se mide en otro equipo.** En este, la medición reflejaría el límite de memoria y la CPU de bajo consumo, no el coste de usar contenedores. Si se mide aquí de todas formas, se anota como tal.
- Con la RAM tan justa, un `docker build` deja el equipo entre 300 y 650 MB libres (medido en la fase 1; tabla del experimento más abajo). Con VS Code y el navegador abiertos es probable que se pagine a disco; esto no se midió.

### Experimento (fase 1): resultados en este equipo

Medido el 2026-10-31 en la Surface Go 2 descrita arriba, con Docker Desktop arrancado. El plan era hacer tres repeticiones de cada caso; **no se hicieron**. El escenario clave ("tras cambiar un `.cs`") se midió dos veces (en el explore y al implementar) y el resto una sola vez. Los tiempos incluyen todo el comando. La memoria se muestreó cada medio segundo: pico del proceso `vmmemWSL` (la máquina virtual de WSL2, limitada a 3 GB) y mínimo de RAM libre en Windows.

| Escenario | Tiempo | Pico de la VM | RAM libre mínima |
|---|---|---|---|
| Descarga de las dos imágenes base (342 + 42 MB comprimidas) | 87,5 s | 2,1 GB | 865 MB |
| `docker build --target test`, en frío (bases ya descargadas) | 95,9 s | 2,5 GB | 517 MB |
| `docker build --target test`, sin cambios | 4,3 s / 4,5 s | 2,4 GB | 472 MB |
| `docker build --target test`, tras cambiar un `.cs` | **44,0 s / 43,5 s** | 2,5 GB | 426 MB |
| `dotnet test` nativo, sin cambios | 29,9 s | (Docker arrancado) | 336 MB |
| `dotnet test` nativo, tras cambiar un `.cs` | **35,6 s** | (Docker arrancado) | 285 MB |
| Dev container | no medido | | |

Desglose del build en frío: extraer las capas del SDK la primera vez (~31 s; ocurre aunque la imagen ya esté descargada), restaurar (17,5 s), compilar (24,8 s) y ejecutar los tests (7,2 s). Tras cambiar código, el `restore` sale de la caché y solo se repiten el build (~27 s) y los tests (~7 s).

**Cómo leerlo:**

- La comparación justa es **tras cambiar código**: el contenedor tarda **×1,2** lo que tarda el nativo (43,5–44,0 s frente a 35,6 s).
- "Sin cambios" no es comparable: Docker no hace nada si nada cambió (4 s), mientras que `dotnet test` siempre ejecuta los tests (30 s).
- El nativo se midió **con Docker arrancado** y el equipo a menos de 340 MB libres, así que está penalizado. Sin Docker sería algo más rápido; no se midió.

**Conclusión, según el criterio propio** (menos del doble y que no estorbe): el ciclo en contenedor tarda menos del doble que el nativo, pero en este equipo **sí estorba**. Docker ocupa unos 2,5 GB y deja el equipo entre 300 y 650 MB libres, así que no puede quedarse arrancado mientras se edita. Se mantiene el flujo **híbrido**: `dotnet` local para iterar, y `docker build --target test` para dar algo por terminado.

### Medición en un runner de GitHub

`add-ci-pipeline` añade el workflow manual [`measure.yml`](../../.github/workflows/measure.yml). Mide en un mismo runner `ubuntu-24.04` los escenarios de la tabla anterior y escribe los tiempos, la versión de la imagen del runner, el SDK, las CPU y la memoria en el resumen de la ejecución. Se lanza con `gh workflow run measure.yml` y se sigue con `gh run watch`.

Un workflow con `workflow_dispatch` solo se puede lanzar cuando está en la rama por defecto. Desde la rama del change, GitHub respondió `HTTP 404: workflow measure.yml not found on the default branch`, así que la medición se hizo después del merge.

**Medido el 2026-10-31, una sola vez** (run 41227837734):
- runner: imagen `ubuntu24` 20261026.354.1, 4 CPU y 15 GB de RAM;
- SDK 10.0.403 y Docker 28.0.4.

La columna del equipo local repite, para comparar, la tabla anterior:

| Escenario | Runner de GitHub | Surface Go 2 |
|---|---|---|
| Descarga de las imágenes base | 18,0 s | 87,5 s |
| `dotnet test` nativo en frío (con restauración) | 17,2 s | no medido |
| `dotnet test` nativo tras cambiar un `.cs` | **5,7 s** | 35,6 s |
| `docker build --target test` en frío (`--no-cache`) | 14,4 s | 95,9 s |
| `docker build --target test` tras cambiar un `.cs` | **8,0 s** | 43,5–44,0 s |

**Cómo leerlo:**

- **Tras cambiar código**, en el runner el contenedor tarda **×1,4** lo que tarda el nativo (8,0 s frente a 5,7 s). En la Surface era ×1,2, pero allí el nativo estaba penalizado por la memoria que ocupaba Docker.
- **En frío**, el contenedor sale más rápido que el nativo (14,4 s frente a 17,2 s). No se ha investigado por qué, así que no se saca ninguna conclusión. Las dos cifras incluyen la restauración de paquetes, pero el nativo es además la primera ejecución de `dotnet` en ese runner.
- El runner es entre **4 y 6 veces más rápido** que la Surface en todos los escenarios comparables. Con 4 CPU y 15 GB, Docker no compite por la memoria.
- La CI en un PR tarda más que estos tiempos (`test`: 51 s en frío y 14 s sin cambios), porque incluye preparar el runner, descargar la caché de GitHub Actions y exportarla.

**Conclusión, según el criterio propio** (menos del doble y que no estorbe): en un equipo con recursos suficientes, trabajar en contenedor cuesta **menos del doble** (×1,4 tras cambiar código) y **no estorba**. El experimento no descarta el flujo "solo contenedor" en un equipo así. En la Surface Go 2 sigue estorbando por la memoria, de modo que el proyecto mantiene el flujo **híbrido**. Es una sola medición en cada equipo: sirve para el orden de magnitud, no como cifra exacta.

## Contenedor y seguridad agéntica

- Un contenedor **no sustituye a los guardrails** (documento 5) ni a la autorización (documento 6). Limita el daño a nivel de sistema; no impide que el agente haga algo permitido pero equivocado dentro de la app.
- **Control de salida de red**: la app solo necesita llegar a la API del modelo. Restringir el tráfico saliente a destinos concretos es una medida de segmentación de red que Microsoft recomienda para funciones de agentes. En compose esto no es trivial (no se filtra por dominio sin un proxy); queda como ejercicio posterior.
- **Sandbox de herramientas**: en este proyecto las tools son *in-memory* y no ejecutan código. Pero si un agente ejecutara comandos o código generado, el contenedor (mejor, uno efímero y sin red) sería la barrera básica. Es un patrón a conocer.
- **Datos**: no se montan datos reales en el contenedor. En el sandbox solo hay datos sintéticos.

## Errores típicos

- Secretos en `ARG`, `ENV` o capas de la imagen: quedan en el historial de la imagen aunque se borren después.
- Imagen base `latest` o sin fijar: construcciones no reproducibles.
- Ejecutar como root.
- Copiar todo el código antes del `restore`: se pierde la caché.
- Olvidar el `.dockerignore`: se cuelan `bin/`, `obj/`, `.git` o, peor, un `.env`.
- Usar un `.dockerignore` de lista negra y confiar en que alguien se acuerde de añadir cada fichero sensible nuevo.
- Copiar los `obj/` del anfitrión al contenedor: `project.assets.json` lleva rutas de Windows y el build falla o mezcla artefactos.
- Añadir un proyecto a la solución y no su `COPY` en la etapa `restore` (aquí falla con `MSB3202`, que al menos es explícito).
- Fijar solo la etiqueta (`10.0.14-noble-chiseled`) sin digest: Microsoft puede reconstruirla con parches del sistema operativo.
- Intentar `docker run -it <imagen> sh` en una imagen *chiseled*: no tiene shell; hay que inspeccionarla desde fuera.
- Cortar la salida de `docker build` con `| Select-Object -First N` en PowerShell: se pierde el código de salida real (pasó en este proyecto al verificar los escenarios negativos).
- Saltos de línea CRLF en scripts o `Dockerfile`: fallan en Linux (`/bin/sh^M`). Por eso la fase 1 incluye `.gitattributes` con `eol=lf`.
- Montar el código desde una carpeta de Windows y quejarse de la lentitud.
- Creer que el contenedor es una frontera de seguridad completa.

## Referencias

- Contenedores con el SDK de .NET (sin Dockerfile): <https://learn.microsoft.com/dotnet/core/containers/sdk-publish>
- Visión general y propiedades necesarias: <https://learn.microsoft.com/dotnet/core/containers/overview>
- Referencia de configuración (imagen base, usuario): <https://learn.microsoft.com/dotnet/core/containers/publish-configuration>
- Tutorial de Dockerfile para .NET (multi-etapa, digest): <https://learn.microsoft.com/dotnet/core/docker/build-container>
- Introducción a .NET y Docker (imágenes, *chiseled*): <https://learn.microsoft.com/dotnet/core/docker/introduction>
- Aspire Dashboard autónomo: <https://learn.microsoft.com/dotnet/core/diagnostics/observability-otlp-example>
- Imágenes de contenedor de .NET (variantes, globalización): <https://learn.microsoft.com/dotnet/core/docker/container-images>
- Limpiar objetos de Docker (`prune`): <https://docs.docker.com/engine/manage-resources/pruning/>
- Nombre canónico del fichero de compose: <https://docs.docker.com/compose/intro/compose-application-model/>
- `RUN --network` en Buildah y Podman: [Containerfile(5)](https://man.archlinux.org/man/Containerfile.5.en)
- Versiones de .NET 10: `https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json`; etiquetas y digests: API del registro de MCR (`https://mcr.microsoft.com/v2/dotnet/<repo>/tags/list`)
- Dev containers con .NET: <https://learn.microsoft.com/dotnet/architecture/microservices/net-core-net-framework-containers/official-net-docker-images>
- Condiciones de licencia de Docker Desktop, WSL2 y herramientas de escaneo: **no verificado**; comprobar la política de tu organización antes de instalar.
