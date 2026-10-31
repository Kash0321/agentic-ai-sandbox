# Design

## Context

Motivación y alcance en `proposal.md`; requisitos en `specs/solution-layout` y `specs/build-standards`.

Estado de partida, observado al inspeccionar el repo y el equipo:

- El repositorio solo tiene documentación y configuración de OpenSpec. No hay `src/`, `tests/`, solución ni `.gitignore`.
- El equipo tiene el SDK 10.0.401. `dotnet new sln` genera por defecto el formato `.slnx`.
- `AGENTS.md` fija las convenciones (nullable, warnings como errores, paquetes centralizados, xUnit, namespaces de ámbito de fichero, primary constructors) y la separación entre dominio, aplicación e invocación del LLM.
- El equipo es modesto (2 núcleos de baja potencia, 8 GB de RAM): conviene que el build y los tests sean ligeros.

**Experimento previo (desechable, fuera del repo).** Para no dejar la elección del marco de pruebas como pregunta abierta, se probó en un directorio temporal una solución `.slnx` con una biblioteca vacía y un proyecto de tests xUnit v3 sobre el SDK 10.0.401. Resultados:

- Sin configuración adicional, `dotnet test` **falla** con xUnit v3: el SDK 10 ya no admite el modo VSTest para Microsoft.Testing.Platform y exige optar por el nuevo modo.
- Añadiendo a `global.json` el bloque `"test": { "runner": "Microsoft.Testing.Platform" }`, `dotnet test` funciona (con `--solution` o sin él).
- Basta el paquete `xunit.v3`: no hizo falta `Microsoft.NET.Test.Sdk` ni `xunit.runner.visualstudio`.
- xUnit v3 no añade `using Xunit` implícito: el proyecto de tests debe declararlo con `<Using Include="Xunit" />`.
- Una biblioteca vacía referenciada por el proyecto de tests se copia a la salida aunque el compilador no use la referencia, así que `Assembly.Load("<nombre>")` funciona. Esto permite escribir pruebas de reglas de capas sin añadir código de producción.

Versiones estables más recientes observadas en NuGet el 2026-10-31: `xunit.v3` 4.0.2 (la línea v2 termina en `xunit` 2.9.3). Se reverifican al implementar.

## Goals / Non-Goals

**Goals:**

- Un esqueleto que compile con cero advertencias y pase sus tests en un clon limpio, con los estándares activos desde el primer día.
- Que las reglas de capas sean comprobables por una prueba automática, no solo por convención.
- El menor número posible de dependencias y de ficheros de configuración.

**Non-Goals:**

- Cualquier lógica de dominio, tool o integración con IA.
- Pruebas de arquitectura con librerías externas (por ejemplo NetArchTest): no se añaden dependencias solo para esto.
- Análisis estático adicional de terceros, cobertura de código o mutación.
- Formato de código automático en CI (lo decide `add-ci-pipeline` si procede).

## Decisions

### D1. Estructura y matriz de referencias

```
 Domain  <---  Application  <---  Agents  <---  Runner
   ^                ^                ^             |
   |                |                |             |
   +----------------+----------------+-------------+
        (Runner referencia a todas: es la raiz de composicion)
```

| Proyecto | Referencia a |
|----------|--------------|
| `Sandbox.Domain` | nada |
| `Sandbox.Application` | `Sandbox.Domain` |
| `Sandbox.Agents` | `Sandbox.Application`, `Sandbox.Domain` |
| `Sandbox.Runner` | `Sandbox.Agents`, `Sandbox.Application`, `Sandbox.Domain` |
| `Sandbox.Domain.Tests` | `Sandbox.Domain` |
| `Sandbox.Agents.Tests` | `Sandbox.Agents`, `Sandbox.Application`, `Sandbox.Domain` |
| `Sandbox.Scenarios.Tests` | `Sandbox.Runner`, `Sandbox.Agents`, `Sandbox.Application`, `Sandbox.Domain` |

Las referencias se declaran **directas** (no se confía en la transitividad) para que la matriz sea legible y verificable con `dotnet list reference`. `Sandbox.Runner` es la raíz de composición: único sitio que conoce todas las capas para registrar dependencias. `Sandbox.Application` no tiene proyecto de tests propio: sus tests nacerán en `Sandbox.Domain.Tests` o `Sandbox.Scenarios.Tests` según su naturaleza, tal como prevé `README.md`.

*Alternativa descartada:* un único proyecto con carpetas por capa. No impide en compilación que el dominio use la IA; con proyectos separados, esa violación exige añadir una referencia explícita y es detectable.

### D2. Cómo se hace cumplir "dominio y aplicación sin IA"

Una clase `LayerRulesTests` en `Sandbox.Scenarios.Tests` (referencia a las cuatro capas) carga los ensamblados de `Sandbox.Domain` y `Sandbox.Application` con `Assembly.Load` y recorre sus ensamblados referenciados, de forma recursiva con un conjunto de visitados. Falla si encuentra alguno cuyo nombre empiece por un prefijo de IA. Lista inicial de prefijos: `Microsoft.Extensions.AI`, `OpenAI`, `Azure.AI`, `Anthropic`, `ModelContextProtocol`. También comprueba que `Sandbox.Application` no referencia `Sandbox.Agents` ni `Sandbox.Runner`, y que `Sandbox.Domain` no referencia ningún otro ensamblado `Sandbox.*`.

Limitación asumida: el compilador omite referencias que el código no usa, así que la prueba detecta dependencias **efectivas**, que son las que importan en ejecución. La dirección de referencias de proyecto se verifica además con `dotnet list reference` como paso de verificación de las tareas, y los ciclos los impide el propio build.

`Sandbox.Domain.Tests` y `Sandbox.Agents.Tests` llevan, de momento, un único test de cableado (el ensamblado bajo prueba se carga con el nombre esperado). Es un marcador honesto: comprueba que la referencia de proyecto funciona, y **se sustituirá por tests reales** en las fases 3 y 4.

*Alternativas descartadas:* NetArchTest u otra librería de reglas (dependencia nueva solo para esto); analizar los `.csproj` como XML (más frágil que inspeccionar ensamblados).

### D3. Marco de pruebas: xUnit v3 sobre Microsoft.Testing.Platform

Se usa `xunit.v3` con el modo Microsoft.Testing.Platform, habilitado en `global.json` (ver el experimento). Los proyectos de tests son ejecutables (`OutputType` `Exe`), declaran `<Using Include="Xunit" />` y no necesitan los paquetes de VSTest.

*Alternativa descartada:* xUnit v2 (`xunit` 2.9.3) con `Microsoft.NET.Test.Sdk` y el runner de Visual Studio. Funcionaría por la vía clásica, pero es la línea en mantenimiento reducido y obliga a más paquetes. Queda como plan B si la integración con el IDE (ver Preguntas abiertas) resulta inviable.

### D4. SDK fijado

`global.json` con `version` `10.0.100` y `rollForward` `latestFeature`, sin versiones preliminares (`allowPrerelease` `false`: fuera de Visual Studio su valor por defecto es `true`, según la documentación), un `errorMessage` que indica cómo obtener el SDK cuando falta (opción disponible desde el SDK 10, añadida durante la implementación) y el bloque `test.runner` de D3. Se exige como mínimo el primer SDK de la serie 10 y se acepta cualquier SDK 10.0.x más reciente: así el contenedor (`sdk:10.0`) y la CI funcionan sin actualizar el fichero en cada parche, y un SDK de la serie 9 falla con un error explícito.

**El mínimo es laxo a propósito.** El repositorio exige solo un SDK 10 y no obliga a nadie a tener la última versión; mantener al día el SDK de una máquina concreta es decisión de su responsable (en el equipo de desarrollo actual está instalado el 10.0.403, la última estable a 2026-10-31). El `Dockerfile` y la CI sí usarán la última imagen estable de la serie 10.0 (ver `AGENTS.md`, sección 11).

*Alternativas descartadas:* fijar `10.0.401` con `latestPatch` (más reproducible, pero frágil: cada actualización del SDK en el contenedor o en la CI obligaría a tocar el fichero) y subir el mínimo a `10.0.403` (garantiza parches, pero obliga a quien clone el repositorio a actualizar su SDK).

### D5. `Directory.Build.props` y analizadores

Valores comunes: `TargetFramework` `net10.0`, `Nullable` `enable`, `ImplicitUsings` `enable`, `TreatWarningsAsErrors` `true`, `EnableNETAnalyzers` `true`, `EnforceCodeStyleInBuild` `true`, `IsPackable` `false`, `LangVersion` sin fijar. Los `.csproj` quedan mínimos (solo referencias y lo propio de cada proyecto).

El nivel de análisis se **fija** a `AnalysisLevel` `10.0-recommended` en lugar de `latest-recommended`, para que una actualización del SDK no añada reglas nuevas que rompan el build por sorpresa; subirlo será una decisión explícita.

### D6. `.editorconfig` mínimo

Raíz (`root = true`), codificación UTF-8, sangría con espacios (4 en C#, 2 en JSON/YAML/XML/Markdown), `insert_final_newline`. Reglas de C#: namespaces de ámbito de fichero como **error**; primary constructors como sugerencia. Para los ficheros de tests se desactiva `CA1707` (guiones bajos en identificadores) porque `AGENTS.md` fija el patrón `Metodo_Condicion_Resultado`.

`end_of_line` **no** se fija aquí: se decidirá junto con `.gitattributes` en `add-container-build`, para evitar ruido de saltos de línea con `core.autocrlf=true` en Windows.

### D7. Paquetes centralizados

`Directory.Packages.props` con `ManagePackageVersionsCentrally` `true`. Contiene solo `xunit.v3` con la versión verificada al implementar. Los `.csproj` referencian el paquete sin versión.

### D8. Proyectos mínimos y entrada del Runner

Las bibliotecas no contienen ficheros de código (se elimina la clase de ejemplo de la plantilla). `Sandbox.Runner` necesita un punto de entrada: un `Program.cs` de una sola sentencia que termina con código 0, a sustituir en la fase 2. Se crean con `dotnet new` y `dotnet sln add` (carpetas de solución `src` y `tests`), y luego se simplifican los `.csproj`.

### D9. `.gitignore`

Plantilla curada, no la completa de Visual Studio: `bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`, `TestResults/`, registros, editores (`.idea/`), `.claude/settings.local.json`, y la protección de secretos: `secrets/`, `.env` y `.env.*` con la excepción `!.env.example`. Los ficheros generados de `.claude/` y `.agents/` **sí** se versionan.

### D10. Documentación de aprendizaje

Documento nuevo `docs/learn/08-estructura-y-estandares-de-la-solucion-dotnet.md`: dirección de dependencias y raíz de composición, por qué proyectos separados, Central Package Management, `global.json` y `rollForward`, analizadores y warnings como errores, `.slnx`, y el caso real de xUnit v3 con MTP descubierto en el experimento. Se amplían el índice y el glosario.

## Risks / Trade-offs

- **Un analizador estricto rompe el build en código legítimo** (por ejemplo, `CA1707` en tests) → se afina solo lo necesario, con cada supresión justificada en el `.editorconfig`, y se fija `AnalysisLevel` para no heredar reglas nuevas con cada SDK.
- **El IDE no descubre los tests con el modo MTP** → la línea de comandos funciona (verificado); si el IDE falla, se evalúa añadir el runner de VSTest o volver a xUnit v2 (D3). No bloquea el change.
- **Un colaborador con un SDK 10 antiguo compila sin los últimos parches** → se acepta: el repositorio no impone la última versión. La CI y el contenedor usan la última estable, y el responsable mantiene al día su propia máquina.
- **Aviso `NU1507` con varias fuentes de NuGet**: con gestión central de paquetes, NuGet avisa si hay más de una fuente configurada, y con warnings como errores eso rompería el build en máquinas con fuentes adicionales (por ejemplo, la de paquetes sin conexión de Visual Studio) → en esta máquina solo hay nuget.org, así que no ocurre. Si aparece, se resuelve con un `nuget.config` del repositorio con correspondencia de fuentes de paquetes, que además aporta reproducibilidad. Queda como mejora posible; no se añade ahora.
- **La auditoría de vulnerabilidades de `dotnet restore` rompe el build con warnings como errores**: en .NET 10 la restauración audita también los paquetes transitivos, y una vulnerabilidad recién publicada en una dependencia puede hacer fallar el build hasta actualizarla → hoy solo hay `xunit.v3`, el riesgo es bajo. Se mantiene el comportamiento por defecto (visibilidad máxima) y se reevaluará si empieza a bloquear PR ajenos al problema.
- **`.slnx` aún no es compatible con alguna herramienta antigua** → el SDK 10 lo genera por defecto; se asume.
- **El test de capas solo ve dependencias efectivas** → se complementa con `dotnet list reference` y con los ciclos que impide el build (D2).
- **Los tests de cableado de `Domain.Tests` y `Agents.Tests` son marcadores** → se documentan como temporales y se sustituyen en las fases 3 y 4; la tarea de cierre lo recuerda.
- **Ruido de saltos de línea hasta que llegue `.gitattributes`** → `end_of_line` se pospone (D6) y el change `add-container-build` va justo después.
- **Primera restauración y tests lentos en este equipo** → en el experimento, restaurar tardó unos 10 s y el test unos 30 a 40 s en frío. Es un dato aproximado y de una sola ejecución; se tendrá en cuenta al medir en `add-ci-pipeline`.

## Open Questions

- ¿Funciona la detección de tests desde el IDE (VS Code con C# Dev Kit) con xUnit v3 en modo MTP? Se probará al implementar; la respuesta solo decide si hace falta añadir un runner adicional.
- Prefijos adicionales para la lista de librerías de IA prohibidas, a medida que aparezcan proveedores nuevos.
