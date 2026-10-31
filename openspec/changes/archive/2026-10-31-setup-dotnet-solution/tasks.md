# Tasks

Rama de trabajo: `feat/setup-dotnet-solution`. Referencias: `design.md` (decisiones D1 a D10) y `specs/`.

## 1. Higiene del repositorio y SDK

- [x] 1.1 Crear `.gitignore` según D9. Verificar con `git check-ignore -v` sobre ficheros temporales que quedan ignorados `bin/x`, `obj/x`, `secrets/x`, `.env`, `.env.local` y `.claude/settings.local.json`, y que **no** lo están `.env.example`, `.claude/skills/openspec-propose/SKILL.md`, `.agents/skills/openspec-propose/SKILL.md` y `openspec/config.yaml`. Borrar los temporales después.
- [x] 1.2 Crear `global.json` según D4 y D3 (`version` `10.0.100`, `rollForward` `latestFeature`, sin preliminares, `test.runner` `Microsoft.Testing.Platform`). El mínimo se mantiene en `10.0.100` a propósito (ver D4). Verificar que `dotnet --version` en la raíz imprime un 10.0.x; cambiar temporalmente la versión a `99.0.100` y comprobar que `dotnet --version` falla indicando que no hay un SDK compatible; restaurar el valor.
- [x] 1.3 Crear `docs/learn/08-estructura-y-estandares-de-la-solucion-dotnet.md` con las secciones de `.gitignore` y protección de secretos, y de `global.json`, `rollForward` y fijación del SDK, siguiendo las convenciones de `docs/learn/README.md`. Añadir la fila al índice de `docs/learn/README.md` y los términos nuevos a `docs/learn/glosario.md`. Verificar que no hay enlaces relativos rotos y que no hay caracteres de flecha no monoespaciados en el documento.

## 2. Build estricto y solución vacía

- [x] 2.1 Crear `Directory.Build.props` (D5), `Directory.Packages.props` (D7) y `.editorconfig` (D6). En `Directory.Packages.props`, `xunit.v3` con la versión estable más reciente, **reverificada** contra NuGet en ese momento. Verificar que los dos ficheros XML están bien formados (por ejemplo, cargándolos con `[xml]` en PowerShell) y que `.editorconfig` tiene `root = true`.
- [x] 2.2 Crear `AgenticSandbox.slnx` y los proyectos `Sandbox.Domain`, `Sandbox.Application`, `Sandbox.Agents` y `Sandbox.Runner` bajo `src/` (D1 y D8), con carpetas de solución `src` y `tests`, `.csproj` mínimos, sin clases de plantilla y con un `Program.cs` de una sola sentencia en el Runner. Verificar con `dotnet build AgenticSandbox.slnx` (cero advertencias, cero errores); con `dotnet msbuild <proyecto> -getProperty:Nullable,TreatWarningsAsErrors,TargetFramework,OutputType` que cada proyecto hereda `enable`, `true` y `net10.0` y que solo el Runner es `Exe`; y con `dotnet list <proyecto> reference` que las referencias coinciden con la matriz de D1.
- [x] 2.3 Comprobar los escenarios negativos de `build-standards` con cambios temporales que se revierten después: una asignación de `null` a un tipo no anulable (el build falla con error), un `PackageReference` con versión inline (la restauración o el build fallan pidiendo versión central) y un namespace con llaves (el build falla por la regla de ámbito de fichero). Verificar que `git status` no muestra rastro de esos cambios.
- [x] 2.4 Ampliar el documento 08 con las secciones de dirección de dependencias y raíz de composición, por qué proyectos separados, Central Package Management, analizadores y warnings como errores (incluido `AnalysisLevel` fijado) y el formato `.slnx`. Añadir los términos al glosario. Verificar enlaces y flechas como en 1.3.

## 3. Proyectos de tests

- [x] 3.1 Crear `Sandbox.Domain.Tests`, `Sandbox.Agents.Tests` y `Sandbox.Scenarios.Tests` bajo `tests/` según D3 y la matriz de D1 (`OutputType` `Exe`, `<Using Include="Xunit" />`, `xunit.v3` sin versión), añadirlos a la solución, y escribir en `Domain.Tests` y `Agents.Tests` el test de cableado (el ensamblado bajo prueba se carga con el nombre esperado) con nombre `Metodo_Condicion_Resultado`. Verificar con `dotnet test` en la raíz que se ejecuta al menos un test por proyecto, todos pasan y no hay advertencias, y que `CA1707` no se dispara con los nombres de test.
- [x] 3.2 Escribir `LayerRulesTests` en `Sandbox.Scenarios.Tests` según D2: `Sandbox.Domain` y `Sandbox.Application` sin referencias, directas ni transitivas, a ensamblados con prefijo de IA; `Sandbox.Application` sin referencia a `Sandbox.Agents` ni `Sandbox.Runner`; `Sandbox.Domain` sin referencia a otros ensamblados `Sandbox.*`. Verificar que `dotnet test` pasa y, como prueba negativa temporal, que añadir al dominio una referencia a `Microsoft.Extensions.AI.Abstractions` usada en código hace fallar la prueba e identifica el ensamblado; revertir y comprobar que `git status` queda limpio de ese cambio.
- [x] 3.3 Actualizar la línea de convenciones de tests de `AGENTS.md` (xUnit v3 sobre Microsoft.Testing.Platform) y, si procede, los requisitos del `README.md`. Ampliar el documento 08 con xUnit v3 sobre Microsoft.Testing.Platform (incluido el caso real de `dotnet test` que falla sin `global.json`), los tests de arquitectura y el aviso de que los tests de cableado son temporales; añadir los términos al glosario. Verificar enlaces y flechas, y que las órdenes de la guía rápida del `README.md` (`dotnet build`, `dotnet test`) funcionan tal como están escritas.

## 4. Integración y cierre

- [x] 4.1 Verificar el clon limpio: copiar el árbol de trabajo, sin `bin/` ni `obj/`, a un directorio temporal y ejecutar allí `dotnet build` y `dotnet test`. Debe terminar con cero advertencias, cero errores y todos los tests en verde.
- [x] 4.2 Verificar el entorno sin red ni secretos: tras restaurar, ejecutar `dotnet build --no-restore` y `dotnet test --no-build` sin ninguna variable de entorno de claves ni secretos de usuario configurados, y comprobar que terminan correctamente.
- [x] 4.3 Actualizar `docs/plan.md` para que la fase 1 refleje la división en los tres changes (`setup-dotnet-solution`, `add-container-build`, `add-ci-pipeline`) y el estado de este. Verificar que los enlaces del plan siguen siendo válidos.
- [x] 4.4 Validar y cerrar: `openspec validate setup-dotnet-solution --strict` sin errores, `dotnet build` sin advertencias y `dotnet test` en verde. Dejar constancia de que la comprobación en contenedor (`docker build --target test`) queda para `add-container-build`, que es quien crea el `Dockerfile`.

## Workflow follow-up

- Abrir el PR de la rama `feat/setup-dotnet-solution` cuando el usuario lo pida; el merge lo hace el usuario con *squash*.
- Archivar el change con `openspec archive setup-dotnet-solution` cuando el usuario lo pida.
- Siguientes changes de la fase 1: `add-container-build` y `add-ci-pipeline`.
- La comprobación en contenedor (`docker build --target test`) no se ha podido hacer en este change porque el `Dockerfile` lo crea `add-container-build`.
- Sin verificar: que el explorador de tests de un IDE (VS Code con C# Dev Kit) descubra los tests con el ejecutor Microsoft.Testing.Platform. Es la primera pregunta abierta de `design.md`; la línea de comandos sí funciona.
