# Proposal

## Why

El repositorio solo contiene documentación: no hay solución .NET, así que nada de lo planificado (dominio dummy, tools, guardrails, tests) puede empezar. Antes de escribir una línea de aplicación hay que fijar un esqueleto **vacío pero verificable**: la estructura de proyectos con la dirección de dependencias que impone `AGENTS.md` (el dominio no conoce `Microsoft.Extensions.AI`) y un build estricto desde el primer día (nullable, warnings como errores, versiones centralizadas, SDK fijado). Es mucho más barato imponer estas reglas ahora, con cero código, que corregirlas después.

Es la **parte 1 de 3 de la fase 1** de `docs/plan.md`. La fase se divide en tres changes para que cada PR sea pequeño y revisable: este (solución), `add-container-build` (Dockerfile, compose, `.dockerignore`, `.gitattributes`) y `add-ci-pipeline` (workflow de CI, check obligatorio en el ruleset y medición nativo frente a contenedor).

Observado al inspeccionar el repo: **no existe `.gitignore`**, aunque el plan de la fase 0 lo listaba. Sin él, `bin/`, `obj/` y, sobre todo, carpetas de secretos o `.env` podrían acabar versionados en un repositorio público. Se corrige aquí.

## What Changes

- **SDK fijado** con `global.json` (SDK 10, con política de `rollForward`).
- **Build estricto común** en `Directory.Build.props`: `net10.0`, `Nullable` habilitado, `ImplicitUsings`, `TreatWarningsAsErrors`, `LangVersion` por defecto, analizadores de .NET activos.
- **Versiones centralizadas** con Central Package Management en `Directory.Packages.props`.
- **`.editorconfig`** mínimo que hace cumplir en el build las convenciones de `AGENTS.md` que un analizador puede comprobar (por ejemplo, namespaces de ámbito de fichero).
- **Solución `AgenticSandbox.slnx`** con cuatro proyectos de código (`Sandbox.Domain`, `Sandbox.Application`, `Sandbox.Agents`, `Sandbox.Runner`) y tres de tests (`Sandbox.Domain.Tests`, `Sandbox.Agents.Tests`, `Sandbox.Scenarios.Tests`), con referencias entre proyectos que respetan la dirección de dependencias.
- **Tests mínimos pero reales** en cada proyecto de tests, incluido uno que protege que el dominio no dependa de librerías de IA.
- **`.gitignore`** para .NET, que además excluye `secrets/`, `.env` y ficheros locales de usuario.
- **Documentación de aprendizaje**: documento nuevo `docs/learn/08-estructura-y-estandares-de-la-solucion-dotnet.md`, glosario e índice actualizados.
- **Plan**: `docs/plan.md` refleja la división de la fase 1 en tres changes.
- **Sin dependencias de IA todavía.** Los únicos paquetes son los de test.

### Fuera de alcance

- `Dockerfile`, `docker-compose.yml`, `.dockerignore` y `.gitattributes`: change `add-container-build`.
- CI, check obligatorio en el ruleset y medición nativo frente a contenedor: change `add-ci-pipeline`.
- Cualquier código de dominio, tools, guardrails, `IChatClient` o integración con DeepSeek (fases 2 a 5).
- Paquetes de `Microsoft.Extensions.AI`, `OpenAI` o similares.
- Empaquetado NuGet y metadatos de publicación.

## Capabilities

### New Capabilities

- `solution-layout`: estructura de proyectos de la solución y dirección permitida de las dependencias entre ellos, incluida la regla de que el dominio no conoce librerías de IA y la existencia de proyectos de tests por capa.
- `build-standards`: estándares de build que toda la solución cumple: SDK fijado, nullable y warnings como errores, versiones de paquetes centralizadas e higiene del repositorio (`.gitignore` que protege secretos y artefactos).

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío.

## Impact

- **Código**: se crean `src/` y `tests/` con siete proyectos vacíos, más ficheros de configuración en la raíz (`global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `AgenticSandbox.slnx`).
- **Dependencias**: solo paquetes de test (marco de pruebas y su runner). Las versiones exactas se fijan y verifican al implementar.
- **Documentación**: `docs/learn/` (documento 08, índice, glosario) y `docs/plan.md`. El `README.md` ya describe esta estructura; solo se revisa que coincida.
- **Cambios posteriores**: `add-container-build` y `add-ci-pipeline` dependen de que el build y los tests funcionen. El `Dockerfile` deberá usar una imagen de SDK compatible con `global.json`.
- **Aprendizaje**: conceptos nuevos que cubre el documento 08: dirección de dependencias entre capas y raíz de composición, Central Package Management, fijación de SDK y `rollForward`, analizadores y warnings como errores, y formato `.slnx`.
