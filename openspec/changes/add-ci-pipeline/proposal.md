# Proposal

## Why

Desde `add-container-build`, la definición de terminado (`AGENTS.md`, sección 8) exige `docker build --target test`, pero solo se ejecuta a mano en el equipo de desarrollo. Ese equipo tiene 8 GB de RAM y se queda con menos de 1 GB libre mientras construye. Nada impide fusionar en `main` algo que no compila, con tests en rojo, con specs inválidas o con una imagen base vulnerable. Además, las versiones fijadas (imágenes por digest, paquetes NuGet) envejecen sin que nadie avise.

Es la **parte 3 de 3 de la fase 1** de `docs/plan.md`, tras `setup-dotnet-solution` y `add-container-build`. Con ella se cumple el criterio de salida de la fase: `docker build --target test` en verde en la CI y su check exigido en el ruleset de `main`.

El change parte de un explore con consultas a fuentes primarias, cuyos resultados recoge `design.md`. Dos hallazgos condicionan el alcance:

- **Un workflow con filtros de ruta no puede ser un check obligatorio.** Si se salta, su check queda "Pending" y bloquea el merge. Por eso la CI se ejecuta siempre, sea cual sea el fichero modificado.
- **Fijar las acciones por SHA es necesario.** En marzo de 2026 se forzaron casi todas las etiquetas de una acción de escaneo muy usada para que apuntaran a código malicioso (fuentes secundarias, sin verificar en el aviso oficial). Por eso todas las acciones se fijan por SHA y el repositorio pasa a exigirlo.

## What Changes

- **Workflow de CI** (`.github/workflows/ci.yml`) en cada PR y en cada push a `main`, con `GITHUB_TOKEN` de solo lectura, acciones fijadas por SHA y sin secretos. Tiene tres jobs, que serán los checks obligatorios:
  - `test`: `docker build --target test` sobre el mismo `Dockerfile`, con caché de capas de BuildKit en GitHub Actions;
  - `image`: construye la imagen final, la ejecuta una vez (debe devolver 0) y la escanea con Grype. Falla si hay vulnerabilidades de severidad alta o crítica **con arreglo disponible**;
  - `openspec`: `openspec validate --all --strict` con la CLI fijada en una imagen propia.
- **Imagen de herramientas de OpenSpec** (`tools/openspec/Dockerfile`): Node LTS fijado por etiqueta y digest, más la CLI de OpenSpec en la misma versión que se usa en local. Sirve para la CI y para ejecutar `openspec` en local sin instalar Node.
- **Workflow manual de medición** (`workflow_dispatch`): compara en el mismo runner `dotnet test` nativo con `docker build --target test`, en frío y en caliente. Es la medición "en otro equipo" que quedó pendiente de `add-container-build`. No es un check obligatorio.
- **Dependabot** (`.github/dependabot.yml`): actualizaciones de versión semanales y agrupadas por ecosistema para NuGet, Docker (imágenes base del `Dockerfile` e imagen de herramientas) y GitHub Actions.
- **Configuración de GitHub**, aplicada con `gh api` y reflejada en el repositorio cuando tenga fichero:
  - ruleset `protect-main` con los tres checks obligatorios, ligados a la app GitHub Actions (`.github/rulesets/protect-main.json`);
  - acciones obligatoriamente fijadas por SHA;
  - alertas de Dependabot y actualizaciones de seguridad activadas.
- **ADR 0002**: política de checks obligatorios y de vulnerabilidades de la imagen, porque afecta a todos los changes futuros.
- **Documentación**:
  - nuevo `docs/learn/09-integracion-continua-y-cadena-de-suministro.md`;
  - `docs/learn/07` con la medición en el runner y la sección de CI implementada;
  - glosario, índice y backlog de `docs/learn/README.md`, `README.md`, `AGENTS.md` (las secciones de CI y contenedores) y `docs/plan.md`. En el plan, además, se corrige el estado de `add-container-build`, que sigue como "pendiente de fusionar".

### Fuera de alcance

- Build y verificación con Podman: el runner lo trae instalado, pero se deja para un change propio.
- Publicar imágenes en un registro, firmarlas, generar SBOM como artefacto o atestaciones de procedencia.
- Subir los resultados del escaneo a *code scanning* (SARIF) y CodeQL.
- Exportar resultados de tests (TRX) como artefactos de la CI.
- Cola de merge (*merge queue*) y exigir que la rama esté al día antes de fusionar.
- Runners propios, matrices de sistemas operativos y arm64.
- Despliegue: sigue fuera del alcance del proyecto.

## Capabilities

### New Capabilities

- `continuous-integration`: verificación automática de cada PR y de `main`. Cubre:
  - los checks que se ejecutan (tests en contenedor, imagen final y su escaneo, validación de specs);
  - su seguridad (permisos mínimos, acciones fijadas por SHA, sin secretos);
  - su obligatoriedad en el ruleset de `main`;
  - la política ante vulnerabilidades;
  - la medición manual.
- `dependency-updates`: cómo se detectan y proponen las actualizaciones de dependencias. Cubre los ecosistemas que vigila Dependabot, su frecuencia y agrupación, las alertas de seguridad, y que las versiones fijadas (digests, SHAs, paquetes) sigan fijadas tras cada actualización.

### Modified Capabilities

Ninguna. `container-build` ya define la imagen y su etapa de tests; este change la ejecuta en la CI sin cambiar sus requisitos.

## Impact

- **Ficheros nuevos**:
  - `.github/workflows/ci.yml` y el workflow de medición;
  - `.github/dependabot.yml`;
  - `tools/openspec/Dockerfile`;
  - `docs/adr/0002-…`;
  - `docs/learn/09-…`.
- **Ficheros modificados**:
  - `.github/rulesets/protect-main.json`;
  - `README.md`, `AGENTS.md` (secciones 8, 10 y 11) y `docs/plan.md`;
  - `docs/learn/07`, `docs/learn/README.md` y `docs/learn/glosario.md`.
- **Configuración externa en GitHub**: ruleset, exigencia de SHA, alertas y actualizaciones de seguridad de Dependabot. Se aplica con `gh api` y en un orden concreto, para no dejar `main` bloqueada. Al revertir, hay que deshacerla también.
- **Dependencias**: ningún paquete NuGet nuevo. Las acciones de GitHub, la versión de Grype, la imagen `node` y la CLI de OpenSpec se fijan con la última versión estable, verificada en su fuente primaria al implementar.
- **Flujo de trabajo**:
  - ningún PR se puede fusionar sin los tres checks en verde;
  - una vulnerabilidad alta o crítica con arreglo en la imagen base bloquea cualquier PR hasta actualizar el digest;
  - aparecerán PRs semanales de Dependabot.
- **Lo que solo se puede observar tras el merge**: Dependabot actúa solo sobre `main`. Su comportamiento real con imágenes fijadas por etiqueta y digest, con acciones fijadas por SHA y con la gestión centralizada de paquetes NuGet se comprueba en los primeros PRs que abra. Queda como seguimiento explícito.
- **Conceptos nuevos y dónde se documentan**:
  - en el documento 09:
    - CI con GitHub Actions y permisos del `GITHUB_TOKEN`;
    - eventos `pull_request` frente a `pull_request_target`;
    - fijación de acciones por SHA y ataques de reetiquetado;
    - checks obligatorios en rulesets y la trampa de los filtros de ruta;
    - Dependabot: versiones frente a seguridad, grupos y periodo de espera;
    - escaneo de vulnerabilidades de imágenes y por qué su resultado no es determinista;
    - caché de BuildKit en la CI;
  - en el documento 07: la medición nativo frente a contenedor en el runner.
