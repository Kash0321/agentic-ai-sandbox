# Proposal

## Why

`AGENTS.md` (sección 10) establece que el contenedor es la referencia para dar algo por terminado y la base de la CI, pero el repositorio todavía no tiene `Dockerfile`: nada se puede construir, probar ni ejecutar en contenedor, y `add-ci-pipeline` no tiene sobre qué apoyarse. A partir de este change, la definición de terminado (sección 8) incluye `docker build --target test`.

Es la **parte 2 de 3 de la fase 1** de `docs/plan.md`, tras `setup-dotnet-solution` (ya fusionado) y antes de `add-ci-pipeline`.

El change parte de un explore con experimentos reales en el equipo de desarrollo. Sus resultados están en `design.md`. Dos de ellos cambian el alcance respecto al plan:

- **Globalización.** La imagen final *chiseled* corre en modo de globalización invariante, mientras que los tests (imagen SDK, con ICU) y el desarrollo local (Windows) no. Los tests no cubrirían el comportamiento que se ejecuta de verdad. Se decide activar el modo invariante en toda la solución.
- **Secretos en compose.** Un secreto de compose cuyo fichero no existe solo produce un aviso y el contenedor arranca igual. Por eso no se declara ningún secreto hasta que la fase 2 lo lea y lo valide.

## What Changes

- **`Dockerfile` multi-etapa** con las etapas `restore`, `build`, `test`, `publish` y `final`:
  - SDK `10.0.403-noble` y runtime `10.0.14-noble-chiseled`, fijadas por etiqueta y digest;
  - `restore` cacheable (solo los ficheros de dependencias);
  - `test` ejecutado **sin red**;
  - imagen final de runtime con usuario no root.
- **`.dockerignore` de lista blanca**: excluye todo y vuelve a incluir solo lo necesario para construir.
- **`compose.yaml`** (nombre canónico de Compose) con el servicio `runner` *one-shot*:
  - sistema de ficheros de solo lectura, `cap_drop: ALL` y `no-new-privileges`;
  - límites de memoria y CPU;
  - **sin red** (`network_mode: none`) y sin secretos declarados.
- **`.gitattributes`**: normalización de saltos de línea, con `eol=lf` en `Dockerfile`, scripts y YAML.
- **Globalización invariante** en `Directory.Build.props` (`InvariantGlobalization=true`), igual en local, en los tests y en la imagen, y con un test que lo comprueba.
- **Documentación**:
  - `README.md`: comandos de contenedor que funcionan hoy y estructura con los nombres reales;
  - `docs/plan.md`: estado de los changes de la fase 1;
  - `docs/learn/07` (diseño real y mediciones), `docs/learn/08` (globalización invariante y saltos de línea), glosario e índice de `docs/learn/README.md`.

### Fuera de alcance

- CI, Dependabot, escaneo de la imagen y check obligatorio en el ruleset: change `add-ci-pipeline`.
- Lectura de la API key y declaración del secreto en compose, y `.env.example`: fase 2.
- Argumento `--scenario` del Runner: fase 4. El `runner` de compose ejecuta el Runner tal como está (devuelve 0).
- Perfil del panel de trazas (Aspire Dashboard): fase 5.
- Dev container y flujo "solo contenedor" como forma de trabajar por defecto.
- Medición en otro equipo y medición en CI: `add-ci-pipeline`. Aquí solo se anota la medición de este equipo.
- Imágenes multiplataforma y verificación en arm64; se fija el índice multiplataforma, pero solo se prueba amd64.
- Prueba con Podman: el diseño evita funciones exclusivas de Docker, pero Podman no está instalado y no se verifica.

## Capabilities

### New Capabilities

- `container-build`: construcción, prueba y ejecución local de la solución en contenedor. Cubre:
  - las etapas de la imagen y el `test` sin red;
  - las imágenes base fijadas;
  - un contexto de build mínimo y sin secretos;
  - la imagen final no root;
  - la ejecución endurecida del `runner` con compose.

### Modified Capabilities

- `build-standards`: se añaden dos requisitos:
  - **globalización invariante** en todos los proyectos;
  - **saltos de línea normalizados** en el repositorio (LF obligatorio en los ficheros que se interpretan en Linux).

## Impact

- **Ficheros nuevos**: `Dockerfile`, `.dockerignore`, `compose.yaml`, `.gitattributes`.
- **Ficheros modificados**: `Directory.Build.props` (una propiedad), un test nuevo en `tests/Sandbox.Scenarios.Tests`, `README.md`, `docs/plan.md`, `docs/learn/07`, `docs/learn/08`, `docs/learn/glosario.md` y `docs/learn/README.md`.
- **Dependencias**: ningún paquete NuGet nuevo. Las imágenes base de MCR se fijan por digest, con las versiones verificadas en la fuente primaria al implementar.
- **Flujo de trabajo**: desde este change, la definición de terminado exige `docker build --target test` en verde. En el equipo actual cuesta unos 44 s tras un cambio de código y deja la RAM libre del equipo por debajo de 1 GB. Docker se arranca solo para verificar.
- **Código de la aplicación**: con el modo invariante, crear culturas concretas (`es-ES`) deja de estar disponible. Hoy no hay código que lo haga; el dominio de la fase 3 no deberá depender de culturas.
- **Cambios posteriores**:
  - `add-ci-pipeline` construye este mismo `Dockerfile` y automatiza la actualización de los digests;
  - la fase 2 abre la red del `runner` y declara el secreto.
- **Conceptos nuevos y dónde se documentan**:
  - en el documento 07: build multi-etapa y caché de capas, BuildKit y `RUN --network`, fijación por digest, imágenes *chiseled*, contexto de build y `.dockerignore` de lista blanca, endurecimiento en compose (`read_only`, `cap_drop`, `no-new-privileges`, `network_mode`) y comportamiento de los secretos de compose;
  - en el documento 08: modo de globalización invariante (`InvariantGlobalization`, `PredefinedCulturesOnly`) y normalización de saltos de línea (`.gitattributes`, `text=auto`, `eol`).
