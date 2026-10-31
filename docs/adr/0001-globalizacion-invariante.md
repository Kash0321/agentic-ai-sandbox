# ADR 0001: Globalización invariante en toda la solución

- **Estado**: aceptada
- **Fecha**: 2026-10-31
- **Change**: `add-container-build` (decisión D6 de su `design.md`)

## Contexto

La imagen final del contenedor usa el runtime de .NET *chiseled* (`noble-chiseled`), que no incluye ICU ni tzdata y define `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` en su configuración. Sin ninguna decisión explícita, la solución se comportaría de tres formas distintas:

| Entorno | Globalización |
|---|---|
| Desarrollo local (Windows) | ICU de Windows, cultura de la máquina (aquí `es-ES`) |
| Tests en contenedor (imagen SDK `noble`) | ICU de Ubuntu |
| Imagen final (*chiseled*) | Invariante |

Los tests no cubrirían el comportamiento que se ejecuta de verdad. Un mismo número se formatea `1.234,50` en este equipo y `1,234.50` en la imagen.

## Decisión

Activar `InvariantGlobalization` en `Directory.Build.props`, de modo que aplique a todos los proyectos, incluidos los de tests. Los tres entornos se comportan igual y el comportamiento queda declarado en el código, no solo en la imagen.

Con esta propiedad, el SDK escribe en el `runtimeconfig.json` tanto `System.Globalization.Invariant: true` como `System.Globalization.PredefinedCulturesOnly: true` (observado al publicar). Con la segunda, **pedir una cultura concreta lanza `CultureNotFoundException`** en vez de devolver una cultura con datos invariantes. Lo comprueba `tests/Sandbox.Scenarios.Tests/GlobalizationTests.cs`.

## Alternativas consideradas

- **Runtime `noble-chiseled-extra`** (con ICU y tzdata): no toca el build, pero la imagen pasa de 42 a 57 MB comprimida y el comportamiento depende de los datos ICU de cada entorno. Windows en local seguiría siendo distinto.
- **Runtime `noble` sin *chiseled***: 81 MB comprimida, con shell y más superficie de ataque. No resuelve la diferencia con Windows.
- **No decidir nada**: deja la discrepancia descrita en el contexto.

## Consecuencias

- **Determinismo**: el mismo resultado en cualquier máquina, en línea con el principio 6 de `AGENTS.md`.
- **El dominio no puede depender de culturas**. A partir de la fase 3, importes, fechas y textos dirigidos al LLM o al usuario se formatean de forma explícita (formatos fijos, ISO 8601), nunca con la cultura de la máquina.
- **Comparaciones de texto** (`Compare`, `IndexOf`, `LastIndexOf`) son siempre ordinales, no lingüísticas, según el documento de diseño del modo invariante del runtime; no se ha probado aquí. Si un escenario necesita ordenar o comparar según reglas lingüísticas, hay que revisar esta decisión.
- **Zonas horarias**: no afectan a esta decisión, pero la imagen *chiseled* tampoco incluye tzdata. Si una fase necesita zonas horarias con nombre, tendrá que resolverlo.
- **Revisión**: si una fase necesita formato o comparación por cultura, se abre un change propio que sustituye este ADR.

## Referencias

- Imágenes de contenedor de .NET y globalización: <https://learn.microsoft.com/dotnet/core/docker/container-images>
- Globalización e ICU en .NET: <https://learn.microsoft.com/dotnet/core/extensions/globalization-icu>
- Opciones de configuración de globalización (`InvariantGlobalization`, `PredefinedCulturesOnly`): <https://learn.microsoft.com/dotnet/core/runtime-config/globalization>
- Diseño del modo invariante en el runtime: <https://github.com/dotnet/runtime/blob/main/docs/design/features/globalization-invariant-mode.md>
- Documento de aprendizaje: [`docs/learn/08-estructura-y-estandares-de-la-solucion-dotnet.md`](../learn/08-estructura-y-estandares-de-la-solucion-dotnet.md), sección de globalización invariante.
