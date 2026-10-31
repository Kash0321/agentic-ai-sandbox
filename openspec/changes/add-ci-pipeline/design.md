# Design

## Context

Motivación y alcance en `proposal.md`; requisitos en `specs/continuous-integration` y `specs/dependency-updates`.

Estado de partida observado el 2026-10-31:

- **Repositorio**:
  - no existe `.github/workflows/`;
  - `.github/rulesets/protect-main.json` refleja el ruleset activo `protect-main` (id 26267641): PR obligatorio, solo *squash*, historial lineal, sin borrado ni *force push*, conversaciones resueltas, y ningún check obligatorio.
- **Configuración de Actions en GitHub** (leída con `gh api`):
  - Actions activado con `allowed_actions: all`;
  - `sha_pinning_required: false`;
  - `default_workflow_permissions: read`;
  - los workflows no pueden aprobar PRs.
- **Seguridad del repositorio**:
  - escaneo de secretos y protección al hacer push activados;
  - alertas de vulnerabilidades desactivadas (la API devuelve 404);
  - actualizaciones de seguridad de Dependabot desactivadas.
- **Contenedor**:
  - el `Dockerfile` tiene las etapas `restore`, `build`, `test` (con `RUN --network=none`), `publish` y `final`, y se construye con BuildKit;
  - `.gitattributes` fuerza LF en cualquier fichero llamado `Dockerfile`, en cualquier carpeta;
  - la CLI de OpenSpec local es la 1.16.0, y los skills de `.claude/` y `.agents/` se generan con esa versión.

### Explore previo: consultas y resultados

No se ejecutó ningún workflow durante el explore: hacerlo exige publicar una rama. Lo que sigue son consultas a fuentes primarias del 2026-10-31, salvo donde se indica.

| Qué | Resultado | Fuente |
|---|---|---|
| Últimas versiones de las acciones | `actions/checkout` v7.0.1, `docker/setup-buildx-action` v4.4.1, `docker/build-push-action` v7.4.0, `anchore/scan-action` v7.4.2 | Releases de GitHub (`releases/latest`) |
| SHA de esas etiquetas | `3d3c42e5…`, `f87e5991…`, `c3c9e263…`, `27805bf3…` (completos en `tasks.md` tras reverificar) | `git/ref/tags` de cada repositorio |
| Tipo de acción | Las cuatro son `node24` y no invocan otras acciones | `action.yml` de cada una en su etiqueta |
| Entradas de `anchore/scan-action` | `fail-build` (por defecto `true`), `severity-cutoff` (`medium`), `only-fixed` (`false`), `grype-version`, `output-format` (`sarif`) | `action.yml` v7.4.2 |
| Grype | v0.121.0 (2026-10-19); licencia de la acción MIT | Releases de `anchore/grype`; API de GitHub |
| Imagen del runner `ubuntu-24.04` | Versión 20261026.354.1: Docker 28.0.4, buildx 0.38.1, Compose 2.38.2, Podman 4.9.3, Node 22.23.4 y .NET SDK 10.0.403 preinstalados | Readme de `actions/runner-images` |
| Node LTS | 26.13.0 (*Lithium*, 2026-10-28) | `nodejs.org/dist/index.json` |
| Imagen `node:26.13.0-alpine` | `sha256:c6ad9e9c1d80f6eb607c26b4b0a132ff7a2344fad1c78ea3baa2a7d4c8de6d94` (2026-10-28) | API de Docker Hub |
| CLI de OpenSpec | `@fission-ai/openspec` 1.16.0, MIT, `engines.node >= 22.12.0` | Registro de npm |
| Checks obligatorios y filtros | Un workflow saltado por `paths` o `branches` deja su check "Pending" y bloquea el merge. Un job saltado por `if:` cuenta como éxito | GitHub Docs, *Troubleshooting required status checks* |
| Dependabot | Límite por defecto de 5 PRs de versión abiertos y periodo de espera por defecto de 3 días. Las actualizaciones de seguridad no esperan y tienen su propio límite de 10 | GitHub Docs, *Dependabot options reference* |
| Dependabot con `etiqueta@digest`, SHA o `Directory.Packages.props` | La documentación oficial no lo describe; las fuentes secundarias se contradicen sobre si actualiza la etiqueta o solo el digest | **(no verificado)** |
| Ataque a `aquasecurity/trivy-action` | Marzo de 2026: se forzaron casi todas sus etiquetas de versión a commits maliciosos | Fuentes secundarias; **(no verificado)** en el aviso oficial |

**Corrección del explore**: se afirmó que Dependabot mantendría una imagen `node` usada con `docker run` dentro del workflow. No se había comprobado, y probablemente no es así: el ecosistema `docker` revisa `Dockerfile` y el de `github-actions` revisa los `uses:` **(no verificado)**. De ahí sale D5.

## Goals / Non-Goals

**Goals:**

- Que los tres checks obligatorios reproduzcan en la CI lo mismo que la definición de terminado pide en local, con el mismo `Dockerfile`.
- Que la CI no tenga credenciales que robar: token de solo lectura, sin secretos y con todo lo de terceros fijado.
- Que activar la protección no deje `main` bloqueada en ningún momento.
- Separar lo que se verifica en el PR de lo que solo se puede observar tras el merge.

**Non-Goals:**

- Optimizar el tiempo de la CI más allá de la caché de capas.
- Ejecutar `dotnet build`/`dotnet test` nativos como check. El contenedor manda (`AGENTS.md`, sección 10), y el modo invariante (ADR 0001) hace que el resultado no dependa del sistema operativo.

## Decisions

### D1. Un workflow `ci` con tres jobs independientes y sin filtros

- **Disparadores**: `pull_request` y `push` a `main`. No hay `paths`, `paths-ignore` ni filtro de rama en `pull_request`, para que los checks se publiquen siempre (spec "Verificación automática de cada cambio").
- **Jobs con nombre estable**: `test`, `image` y `openspec`. El nombre del check es el identificador del job y el ruleset depende de él.
- **Sin dependencias entre jobs** (`needs`): corren en paralelo, y un fallo de uno no oculta el resultado de los otros.
- **`concurrency`** por workflow y referencia, con `cancel-in-progress` solo en PRs: un push nuevo cancela la ejecución anterior, pero en `main` se ejecutan todas.
- **`timeout-minutes`** en cada job, para que un bloqueo no consuma los minutos por defecto (6 h).
- **Runner `ubuntu-24.04`** explícito, no `ubuntu-latest`, por la misma razón que no se usa `latest` en imágenes.

Alternativa descartada: un único job que lo haga todo. Construye una sola vez, pero un único check no dice qué falló.

### D2. Seguridad del workflow

- **`permissions: contents: read`** en el nivel del workflow; ningún job necesita más. Si un día hiciera falta, se declara en ese job con su motivo (spec "Permisos mínimos y sin secretos").
- **`actions/checkout` con `persist-credentials: false`**: el token no queda guardado en `.git/config` para los pasos siguientes.
- **Solo `pull_request`, nunca `pull_request_target`**: con `pull_request`, un PR de un fork se ejecuta con token de solo lectura y sin secretos. `pull_request_target` ejecuta en el contexto del repositorio base y es la vía clásica de robo de secretos.
- **Acciones por SHA completo** con `# vX.Y.Z` al lado, y el ajuste `sha_pinning_required` activado en el repositorio (D8). Las cuatro acciones son `node24` sin acciones anidadas, así que el ajuste no rompe nada por esa vía.
- **Sin secretos**: ningún job usa `secrets.*`. La caché de BuildKit (`type=gha`) usa el token interno del runner, no el `GITHUB_TOKEN`. Que funcione con `contents: read` está **(no verificado)** y se comprueba en las tareas.

### D3. Job `test`: el mismo `Dockerfile`, con caché de capas

- `docker/setup-buildx-action` (driver `docker-container`) y `docker/build-push-action` con `target: test`, `push: false` y sin `load`: lo que importa es que los `RUN` terminen bien.
- **Caché**: `cache-from: type=gha` y `cache-to: type=gha,mode=max`. Con `mode=max` también se guardan las capas de las etapas intermedias (`restore`, `build`), que son las que ahorran tiempo.
- **Un `scope` por job** (`test` e `image`). Ajuste en el apply: en el diseño compartían `scope`, pero según la documentación de Docker (*GitHub Actions cache*, consultada el 2026-10-31) cada build sobrescribe la caché de su `scope` y solo queda la del último, así que dos jobs paralelos se pisarían y cada uno perdería las capas propias del otro. Se duplica en caché la parte común (`restore`, `build`) a cambio de no depender de qué job termina el último.
- **Consecuencia de cachear la etapa `test`**: si no cambia ninguna entrada, el paso de los tests sale de la caché y no se vuelven a ejecutar, igual que en local. Se acepta porque los tests son deterministas: con las mismas entradas, el mismo resultado.
- `RUN --network=none` funciona igual con BuildKit en el runner (Docker 28.0.4, buildx 0.38.1).

Alternativa: `docker build` directo, sin acciones. Es una dependencia menos, pero sin la caché de GitHub Actions cada ejecución empieza en frío (unos 96 s más la descarga en el equipo local).

### D4. Job `image`: construir, ejecutar y escanear

1. `build-push-action` con `target: final`, `load: true` y una etiqueta local (`agentic-sandbox-runner:ci`), con la misma caché.
2. **Ejecución de prueba** endurecida como el `runner` de compose: `docker run --rm --read-only --cap-drop ALL --security-opt no-new-privileges --network none agentic-sandbox-runner:ci`, que debe devolver 0. Se usan las mismas restricciones para que la CI detecte también una escritura en disco que el endurecimiento rompería.
3. **Escaneo** con `anchore/scan-action`:
   - `image: agentic-sandbox-runner:ci`;
   - `fail-build: true`, `severity-cutoff: high`, `only-fixed: true`;
   - `grype-version` fijado (v0.121.0, reverificado al implementar);
   - `output-format: table`, para que el resultado se lea en el registro sin subir SARIF.

   Con `only-fixed`, el check solo falla cuando hay algo que hacer: actualizar el digest o el paquete.

   **Ajuste en el apply (2026-10-31)**: según la documentación de la acción (`action.yml` y README de v7.4.2), `only-fixed` hace que *solo se informen* las vulnerabilidades con arreglo. Las demás desaparecerían del registro, lo que contradice la spec. Por eso el escaneo son dos pasos con la misma versión de Grype:
   1. un informe completo (`fail-build: false`, sin `only-fixed`, `output-format: table`), que nunca falla;
   2. el control (`severity-cutoff: high`, `only-fixed: true`, `fail-build: true`).

   La tabla aparece en el registro porque la acción la imprime cuando el formato es `table`.

**El resultado no es determinista**: la base de datos de vulnerabilidades se descarga en cada ejecución, y un CVE publicado hoy pone en rojo un commit que ayer estaba en verde. Es lo que se busca, y la política y sus consecuencias se registran en el ADR 0002.

Alternativas del escáner (decididas en el explore):
- Trivy: mismo modelo, pero sufrió el ataque de reetiquetado de marzo de 2026.
- Docker Scout: exige credenciales de Docker Hub, es decir, un secreto en la CI.

Alternativa de política descartada: solo informar con SARIF. Necesita `security-events: write` y no protege `main`.

### D5. Job `openspec`: imagen de herramientas propia

`tools/openspec/Dockerfile`:
- `FROM node:26.13.0-alpine@sha256:…`: el Node LTS actual, por etiqueta y digest;
- `RUN npm install -g @fission-ai/openspec@1.16.0`, en la misma versión que en local;
- `USER node`;
- `WORKDIR /repo`;
- `ENTRYPOINT ["openspec"]`.

En la CI se construye con `docker build tools/openspec` y se ejecuta con el repositorio montado en solo lectura y sin red:

```
docker run --rm --network none -v "$PWD:/repo:ro" openspec-cli validate --all --strict
```

**Telemetría (observado en el apply, 2026-10-31)**: la CLI 1.16.0 **sí** la tiene. Al ejecutarse imprime "OpenSpec collects anonymous usage stats. Opt out: OPENSPEC_TELEMETRY=0 or openspec config set telemetry.enabled false". Hay dos barreras:
- la imagen define `ENV OPENSPEC_TELEMETRY=0`, con lo que el aviso desaparece;
- en la CI se ejecuta sin red.

También observado: con el repositorio montado en solo lectura, la validación no escribe ningún fichero en el contenedor (comprobado con `find -newer` sobre un fichero marcador).

- **Por qué una imagen propia**:
  - Dependabot (ecosistema `docker`, directorio `/tools/openspec`) mantiene el digest de `node`;
  - Node queda fijado (el del runner cambia con cada imagen);
  - y sirve en local para validar sin instalar Node.
- **Por qué la versión de OpenSpec se sube a mano**: al subirla hay que ejecutar `openspec update` y regenerar los skills, en su propia rama de mantenimiento (`AGENTS.md`, sección 9). Si Dependabot la subiera sola, la CI validaría con una versión distinta de la que generó los skills.
- `.dockerignore` de la raíz no afecta a este build, cuyo contexto es `tools/openspec/` y solo contiene el `Dockerfile`.

Alternativas (decididas en la propuesta): `docker run` de `node` dentro del workflow (nada se actualiza solo), y `package.json` con `npm ci` (desincroniza la CLI de los skills y añade ficheros de Node a la raíz).

### D6. Workflow manual de medición

`.github/workflows/measure.yml`, solo con `workflow_dispatch` y `contents: read`. Usa el SDK preinstalado del runner (10.0.403 en la imagen consultada; `global.json` lo admite) y no usa caché de GitHub Actions.

Pasos, cada uno cronometrado:
1. descarga de las imágenes base;
2. `dotnet test` en frío, con restauración;
3. `dotnet test` tras modificar un `.cs`;
4. `docker build --target test --no-cache`, en frío con las bases ya descargadas;
5. `docker build --target test` tras modificar el mismo `.cs`.

Los tiempos se escriben en una tabla en `$GITHUB_STEP_SUMMARY`, junto con la versión de la imagen del runner (`ImageVersion`), `dotnet --version`, el número de CPU y la memoria. La modificación del `.cs` vive solo en el runner y nunca se commitea.

Son los mismos escenarios que la tabla del documento 07, para poder comparar. Se ejecuta al menos una vez en la rama del change y el resultado se anota en el documento 07 como una sola medición.

### D7. Dependabot

`.github/dependabot.yml`, versión 2:

| Ecosistema | Directorios | Qué vigila |
|---|---|---|
| `nuget` | `/` | `Directory.Packages.props` (hoy solo `xunit.v3`) |
| `docker` | `/` y `/tools/openspec` | Imágenes base del `Dockerfile` y de la imagen de herramientas |
| `github-actions` | `/` | Los `uses:` de `.github/workflows/` |

- **Frecuencia** semanal, con día y zona horaria explícitos (lunes, `Europe/Madrid`).
- **Un grupo por ecosistema** (`patterns: ["*"]`): un PR por ecosistema y semana como máximo.
- Se mantienen el periodo de espera y el límite de PRs por defecto.
- Las actualizaciones de seguridad no pertenecen a esos grupos y llegan cuando se publican.
- Los PRs de Dependabot pasan los mismos checks. No necesitan change de OpenSpec mientras no cambien comportamiento (`AGENTS.md`, sección 6). Uno que lo cambie, por ejemplo una versión mayor de xUnit que rompa tests, se trata como cualquier otro cambio.

### D8. Configuración de GitHub y orden de activación

Orden pensado para no dejar `main` sin protección ni bloqueada:

```
 1. PR con los workflows      --> los 3 checks se ejecutan y se ven
          |                       con su nombre real
          v
 2. verificaciones negativas  --> commits temporales revertidos
          |
          v
 3. sha_pinning_required=on   --> se reejecuta la CI; prueba negativa
          |                       con una accion fijada por etiqueta
          v
 4. ruleset + required checks --> PUT del JSON versionado; el propio
          |                       PR debe seguir pudiendo fusionarse
          v
 5. alertas y security        --> PUT vulnerability-alerts y
    updates                       automated-security-fixes
```

- **Ruleset**: se añade a `protect-main.json` una regla `required_status_checks` con los contextos `test`, `image` y `openspec`, cada uno con el `integration_id` de la app GitHub Actions. Ese id se lee de los *check runs* reales del PR (`app.id`) en vez de copiarlo de la documentación. Lleva `strict_required_status_checks_policy: false`, porque con solo *squash* y un único desarrollador exigir la rama al día no aporta. Se aplica con `gh api -X PUT repos/{owner}/{repo}/rulesets/26267641 --input …`.
- **Durante el PR**, `main` ya tiene el ruleset nuevo y su JSON todavía es el antiguo. Se acepta: "el estado real manda sobre el JSON" (`AGENTS.md`, sección 9), y el merge lo iguala.
- **Lo que no tiene fichero** (exigencia de SHA, alertas, actualizaciones de seguridad) se documenta en el documento 09 y en `AGENTS.md`, con el comando para comprobarlo.
- **Marcha atrás**:
  - reaplicar el JSON anterior del ruleset;
  - `sha_pinning_required: false`;
  - `DELETE` de `automated-security-fixes` y de `vulnerability-alerts`.

### D9. ADR 0002: checks obligatorios y política de vulnerabilidades

Trasciende el change: obliga a todos los PRs futuros y define cuándo una vulnerabilidad bloquea el trabajo. Se registra en `docs/adr/0002-checks-obligatorios-y-politica-de-vulnerabilidades.md`, con las alternativas de D4 y la consecuencia de que un CVE con arreglo bloquea cualquier PR hasta actualizar.

### D10. Qué se verifica y cuándo

| Requisito | En el PR | Tras el merge |
|---|---|---|
| Checks `test`, `image`, `openspec`, en verde y en rojo | Sí, con commits temporales revertidos | — |
| Ejecución en push a `main` | — | Primer push tras el merge |
| PR solo de documentación | Sí: un commit de documentación del propio change | — |
| Permisos de solo lectura y sin secretos | Revisión del YAML y del token efectivo en el registro del job | — |
| PR desde un fork | No hay ningún fork disponible: se razona en D2 y queda **(no verificado)** | Si llega un PR externo |
| Exigencia de SHA | Sí (paso 3 de D8) | — |
| Ruleset | Sí: el PR muestra los checks como obligatorios | Merge real |
| Política de vulnerabilidades | Parcial: el escaneo en verde con la imagen actual y una ejecución temporal con `severity-cutoff: low` sin `only-fixed` para ver que bloquea; si no hay hallazgos, **(no verificado)** | — |
| Dependabot (configuración) | GitHub valida `dependabot.yml` al subirlo **(no verificado)** | Pestaña de Dependabot sin errores |
| Dependabot (forma de fijación, agrupación) | — | Primeros PRs de Dependabot: seguimiento explícito, no se marca como hecho |
| Alertas y actualizaciones de seguridad | Estado leído con `gh api` | Primera alerta real, si llega |
| Medición | Una ejecución manual en la rama | — |

### D11. PR en borrador antes de archivar: excepción para este change

La CI solo se puede probar en GitHub: el evento `pull_request` y el paso 4 de D8 necesitan un PR abierto, y `workflow_dispatch` solo funciona con workflows que ya están en `main`. Eso choca con `AGENTS.md` (secciones 6 y 9: el PR se abre con el change verificado y archivado).

**Decisión del responsable** (2026-10-31): excepción explícita para este change.
- En cuanto los workflows estén listos, se publica la rama y se abre un **PR en borrador**.
- El apply puede hacer los *push* que necesite en la rama del change.
- El PR pasa a "listo para revisión" solo tras `/opsx:verify` y `/opsx:archive`.
- El merge sigue siendo del responsable.

La regla general de `AGENTS.md` no se modifica.

## Risks / Trade-offs

- **[`main` bloqueada por un nombre de check mal escrito]** → Se aplica el ruleset después de ver los checks reales en el PR, y el `integration_id` se lee de esos checks. Si algo falla, se reaplica el JSON anterior.
- **[CVE con arreglo que bloquea PRs ajenos]** → Es la política elegida (ADR 0002). La salida es actualizar el digest, a mano o con el PR de seguridad de Dependabot.
- **[El escaneo depende de servicios externos]** (descarga de la base de datos de Grype) → Si falla, el check queda en rojo y se reejecuta. No hay alternativa sin red.
- **[Dependabot no actualiza `etiqueta@digest` como esperamos]** (solo el digest, o nada) → Se observa en los primeros PRs. Si no sirve, se documenta y se mantiene la actualización manual de la etiqueta, como hasta ahora.
- **[Dependabot y la gestión centralizada de NuGet]** **(no verificado)** → Mismo tratamiento: seguimiento tras el merge.
- **[Node 26 con OpenSpec 1.16.0]**: el paquete declara `>= 22.12`, pero no se ha probado con Node 26 → El job `openspec` lo comprueba. Si falla, se baja al LTS anterior (24.x) y se anota.
- **[Caché envenenada desde un PR]** → La caché de GitHub Actions se aísla por rama: un PR no puede escribir la caché que lee `main` **(no verificado en este repositorio)**. Además, las imágenes de la CI no se publican.
- **[Divergencia entre la CI y el equipo local]**: la CI usa Docker 28.0.4 y buildx 0.38.1; el equipo local, Docker 29.9.2 → Ninguna función usada depende de la versión. Si aparece una diferencia, manda la CI, que es la referencia compartida.
- **[Minutos de Actions]** → El repositorio es público y los runners estándar no tienen coste **(no verificado para este repositorio concreto)**. Aun así, `timeout-minutes` y `concurrency` evitan derroche.
- **[Medición poco representativa]**: una sola ejecución en un runner compartido → Se anota como tal y se compara solo el orden de magnitud con la del equipo local.

## Migration Plan

1. Implementar en `feat/add-ci-pipeline` y abrir el PR para que la CI se ejecute.
2. Seguir el orden de D8 para la configuración de GitHub.
3. Merge con los tres checks en verde.
4. Comprobar el primer push en `main` y, en las semanas siguientes, los PRs de Dependabot. Anotar lo observado en el documento 09.

**Marcha atrás**: revertir el commit de los workflows y deshacer la configuración externa con los comandos de D8. El ruleset debe volver antes que los workflows: un check obligatorio que ya no se publica bloquea `main`.

## Open Questions

- Si Dependabot actualiza la etiqueta además del digest, y si respeta la gestión centralizada de NuGet. Se responde con sus primeros PRs; no cambia el diseño, solo cómo se documenta la actualización manual.
