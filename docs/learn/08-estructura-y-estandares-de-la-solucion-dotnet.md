# 8. Estructura y estándares de la solución .NET

> **Estado**: en construcción. Este documento crece con el change `setup-dotnet-solution`; ahora cubre `.gitignore`, `global.json`, la estructura en capas, la gestión central de paquetes, los analizadores, `.slnx` y los tests (xUnit v3 sobre Microsoft.Testing.Platform). El change `add-container-build` añade los saltos de línea (`.gitattributes`) y la globalización invariante. Seguirá creciendo con las fases posteriores. **Verificado**: 2026-10-31 contra Microsoft Learn (`global.json`) y con pruebas reales en este repositorio. Lo que viene de conocimiento general de Git y no se contrastó con su documentación se marca **(no verificado)**.

## Qué cubre

Los cimientos del repositorio, antes de escribir ninguna línea de aplicación: qué se ignora en Git y por qué, y cómo se fija el SDK de .NET. Las secciones siguientes (proyectos y dependencias entre capas, versiones centralizadas, analizadores, tests) se añaden al implementar los grupos 2 y 3 del change.

## `.gitignore` y protección de secretos

### Qué es

Un fichero con patrones que le dicen a Git qué ficheros **no** debe ofrecer para versionar. No borra nada ni protege lo que ya está versionado: un fichero que Git ya sigue (`git add`) sigue seguido aunque después se añada a `.gitignore` **(no verificado: comportamiento general de Git)**.

### Por qué importa aquí

Este repositorio es **público**. Un secreto que se sube por error (una API key de DeepSeek, un `.env`) queda en el historial de Git aunque se borre en un commit posterior, y hay que darlo por comprometido y **rotarlo**. La mejor defensa es que sea difícil subirlo por accidente: ignorar por defecto todo lo que pueda contener secretos.

### Qué ignora el proyecto

| Patrón | Qué protege o evita |
|--------|--------------------|
| `bin/`, `obj/`, `artifacts/`, `TestResults/` | Salida de compilación y de tests: ruido que no debe versionarse |
| `.vs/`, `.idea/`, `*.user`, `*.suo` | Configuración local de editores |
| `*.log` | Registros |
| `secrets/` | Carpeta de secretos locales (por ejemplo, la clave de DeepSeek para el contenedor; ver documento 7) |
| `.env`, `.env.*` | Variables de entorno locales, que suelen contener claves |
| `!.env.example` | **Excepción**: la plantilla sin valores sí se versiona |
| `.claude/settings.local.json` | Configuración local del asistente |
| `Thumbs.db`, `desktop.ini`, `.DS_Store` | Ficheros del sistema operativo |

Los skills y comandos generados en `.claude/` y `.agents/` **sí** se versionan: son parte del flujo de trabajo del proyecto.

### La trampa del orden de las reglas

`.env.*` ignora `.env.local`, pero también ignoraría `.env.example`, que queremos versionar. Por eso la excepción `!.env.example` va **después**: en Git, entre las reglas que coinciden con un fichero, **gana la última** **(no verificado: comportamiento general de Git)**. Se comprobó en este repositorio con ficheros temporales: `.env` y `.env.local` quedan ignorados por sus reglas, y `.env.example` queda versionable.

### Cómo comprobarlo

```bash
git check-ignore -v ruta/del/fichero
```

Si el fichero está ignorado, imprime la regla y la línea del `.gitignore` que lo hace; si no lo está, no imprime nada y termina con código distinto de cero. Es la forma fiable de probar una regla en lugar de fiarse de leerla.

### Errores típicos

- Añadir `.env` a `.gitignore` **después** de haberlo subido: ya está en el historial.
- Ignorar `.env*` sin excepción y perder la plantilla `.env.example`.
- Ignorar `*.json` o carpetas enteras y esconder configuración legítima.
- Usar `git add -f` y saltarse la protección.

## `global.json`: qué SDK usa el repositorio

### SDK, runtime y framework de destino: tres cosas distintas

| Concepto | Qué es | Dónde se fija |
|----------|--------|---------------|
| **SDK** de .NET | Las herramientas: compilador, CLI (`dotnet build`, `dotnet test`), MSBuild | `global.json` |
| **Runtime** de .NET | Lo que ejecuta la aplicación ya compilada | Lo que haya instalado en la máquina que la ejecuta |
| **Framework de destino** (TFM) | Para qué versión de .NET se compila la aplicación, por ejemplo `net10.0` | El proyecto (aquí, `Directory.Build.props`) |

La documentación lo dice así: elegir la versión del SDK es **independiente** de la versión del runtime a la que apunta un proyecto. Con el SDK 10 se puede compilar para un framework de destino anterior.

### Qué hace

Sin `global.json`, `dotnet` usa el **SDK más alto instalado** en la máquina, y fuera de Visual Studio eso incluye versiones preliminares. Con `global.json` se acota el rango aceptable. Microsoft señala que, si solo quieres usar siempre el último SDK instalado, no hace falta el fichero, y que en integración continua es donde conviene acotar el rango.

### Los campos que usamos

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false,
    "errorMessage": "Este proyecto necesita el SDK de .NET 10 (10.0.100 o posterior): https://dotnet.microsoft.com/download/dotnet/10.0"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- **`version`**: debe ser la versión **completa**; no admite `10`, `10.0`, comodines ni rangos. Actúa como **versión mínima** aceptada, que aquí llamamos "el suelo".
- **`rollForward`**: qué hacer cuando esa versión exacta no está. Valores relevantes:

  | Valor | Comportamiento |
  |-------|----------------|
  | `disable` | Exige la versión exacta |
  | `patch` (el valor por defecto si hay `version`) | Esa versión o el último parche de la misma banda de características |
  | `latestPatch` | El último parche instalado de la misma banda, igual o superior a la versión pedida |
  | `latestFeature` | La banda de características y el parche **más altos instalados** dentro de la misma versión mayor y menor, igual o superiores a la pedida |
  | `latestMajor` | El SDK más alto instalado, igual o superior a la versión pedida |

  Un SDK tiene la forma `x.y.znn`: `x` mayor, `y` menor, `z` banda de características y `nn` parche. El 10.0.403 es la banda 4, parche 03.
- **`allowPrerelease`**: si se consideran versiones preliminares. **Fuera de Visual Studio su valor por defecto es `true`**, por eso lo fijamos a `false` de forma explícita: sin eso, un SDK preliminar instalado podría usarse sin darnos cuenta.
- **`errorMessage`** (disponible desde el SDK 10): el mensaje que se muestra cuando no hay un SDK compatible. Aquí sirve para decir cómo conseguirlo.
- **`test.runner`** (disponible desde el SDK 10): con qué ejecutor se descubren y ejecutan los tests. Es necesario para xUnit v3; se explica con detalle cuando se añadan los proyectos de tests.

### Qué significa nuestra elección

`version` `10.0.100` con `latestFeature` acepta **cualquier SDK 10.0.x desde el primero de la serie** y usa el más alto que haya instalado. Ejemplos:

| SDK instalados en la máquina | `dotnet` usa | Resultado |
|------------------------------|--------------|-----------|
| 10.0.401 y 10.0.403 | 10.0.403 | Compila |
| Solo 10.0.401 | 10.0.401 | Compila (puede carecer de los últimos parches) |
| Solo 9.0.x | ninguno | Falla con error explícito |

El suelo es **laxo a propósito**: el repositorio no obliga a nadie a tener la última versión. Quien quiera mantener su máquina al día lo hace por su cuenta; la CI y el contenedor sí usarán la última imagen estable de la serie (ver `AGENTS.md`, sección 11). La alternativa, subir el suelo a la última versión, garantizaría los parches pero obligaría a todo el que clone el repositorio a actualizar su SDK.

### Qué se observó en la práctica

Con `version` cambiado a `99.0.100` (que no existe), `dotnet --version` falla con código de salida 155. Antes de añadir `errorMessage` el texto era genérico ("A compatible .NET SDK was not found"); después, `dotnet` muestra nuestro mensaje con el enlace de descarga. Con el valor correcto, el SDK activo en esta máquina es el 10.0.403.

### Dónde busca el fichero

El ejecutor `dotnet` empieza en el **directorio actual** y sube por los directorios padre hasta encontrar un `global.json`. Por eso `dotnet --version` dentro del repositorio puede dar un resultado distinto de ejecutarlo en otra carpeta. El resolvedor de SDK de MSBuild, en cambio, empieza en el directorio de la solución.

### Errores típicos

- Escribir `"version": "10.0"`: es inválido, exige la versión completa.
- Creer que `global.json` fija el runtime o el framework de destino: solo elige el SDK.
- Olvidar `allowPrerelease` y compilar sin querer con un SDK preliminar.
- Fijar el suelo en la última versión y bloquear a quien tenga una anterior sin necesidad.
- Poner `rollForward` `disable` salvo que haya un motivo concreto, como archivos de bloqueo de paquetes.

## Estructura de la solución: capas y dirección de dependencias

### Los proyectos

| Proyecto | Para qué | Qué contendrá |
|----------|----------|---------------|
| `Sandbox.Domain` | El negocio | Entidades, reglas y estados del dominio dummy |
| `Sandbox.Application` | Los casos de uso | Comandos, consultas y servicios in-memory |
| `Sandbox.Agents` | La invocación del LLM | Tools, pipeline de `IChatClient`, guardrails (aquí vive `Microsoft.Extensions.AI`) |
| `Sandbox.Runner` | El arranque | Host de consola, inyección de dependencias, configuración |

### La regla: las dependencias apuntan hacia el dominio

```
 Domain  <---  Application  <---  Agents  <---  Runner
```

Cada proyecto solo puede conocer a los de su izquierda. El `Runner` referencia a los tres porque es la **raíz de composición**: el único sitio que conoce todas las capas para ensamblarlas (registrar servicios, leer configuración). El resto no se conoce entre sí más de lo necesario.

Es la idea de arquitectura por capas con inversión de dependencias, también llamada arquitectura limpia o hexagonal **(no verificado: terminología general de arquitectura de software)**. Aquí tiene una razón muy concreta, que viene de `AGENTS.md`: **las reglas de negocio no pueden depender de la IA**. Si el dominio no sabe que existe un LLM, se puede probar sin red ni clave de API, y el proveedor de modelos queda como un detalle intercambiable de `Sandbox.Agents`.

### Por qué proyectos separados y no carpetas

Con carpetas dentro de un mismo proyecto nada impide que una clase del dominio use `Microsoft.Extensions.AI`: la regla sería solo una convención. Con proyectos separados, esa violación exige **añadir una referencia explícita** al proyecto del dominio, algo que se ve en la revisión y que una prueba automática detecta (se añade en el grupo de tests del change). Además, los ciclos son imposibles: se comprobó que añadir al dominio una referencia al proyecto de aplicación (que ya referencia al dominio) rompe el build con el error `MSB4006` (dependencia circular).

### Referencias directas

Cada proyecto declara **todas** las referencias que usa, aunque algunas también le llegarían de forma transitiva a través de otro proyecto. Así la matriz es legible y se puede comprobar con `dotnet list <proyecto> reference`, que es lo que se hizo al crear la solución:

| Proyecto | Referencia a |
|----------|--------------|
| `Sandbox.Domain` | nada |
| `Sandbox.Application` | `Sandbox.Domain` |
| `Sandbox.Agents` | `Sandbox.Application`, `Sandbox.Domain` |
| `Sandbox.Runner` | `Sandbox.Agents`, `Sandbox.Application`, `Sandbox.Domain` |

### Errores típicos

- Hacer que el dominio dependa de una librería de infraestructura "solo para esta utilidad".
- Poner la lógica de arranque dentro de la capa de aplicación en vez de en la raíz de composición.
- Referencias transitivas implícitas que ocultan la arquitectura real.

## Gestión central de paquetes (Central Package Management)

### Qué es

Un único fichero, `Directory.Packages.props`, declara la versión de cada paquete NuGet (`PackageVersion`) con la propiedad `ManagePackageVersionsCentrally` activada. Los proyectos referencian el paquete **sin versión** (`PackageReference` sin `Version`).

### Por qué

Una versión en un solo sitio: no hay dos proyectos con versiones distintas del mismo paquete por descuido, y actualizar es tocar un fichero. Es además lo que automatizará Dependabot en el change de la CI.

### Qué se observó

Un `PackageReference` con versión escrita en el `.csproj` falla en la restauración con el error `NU1008`, que explica que los proyectos con gestión central deben definir la versión en un `PackageVersion`. Es exactamente el comportamiento que exige la spec.

### Matices de la documentación

- Existe `VersionOverride` para cambiar la versión de un paquete en un proyecto concreto. No lo usamos.
- Con gestión central, NuGet emite el aviso `NU1507` si hay **más de una fuente** de paquetes configurada. Con warnings como errores, eso rompería el build en una máquina con fuentes extra (por ejemplo, la de paquetes sin conexión de Visual Studio). En esta máquina solo hay nuget.org y no ocurre; si ocurriera, la solución es un `nuget.config` en el repositorio con *package source mapping*, que además da reproducibilidad.
- `dotnet restore` en .NET 10 audita también los paquetes transitivos en busca de vulnerabilidades conocidas. Con warnings como errores, una vulnerabilidad recién publicada en una dependencia puede romper el build hasta que se actualice **(los códigos concretos de estos avisos no se verificaron en esta sesión)**. Es un compromiso entre seguridad y estabilidad que conviene tener presente.

### Errores típicos

- Escribir la versión en el `.csproj` por costumbre.
- Olvidar añadir el `PackageVersion` al incorporar un paquete nuevo.
- Versiones preliminares sin querer: la política del proyecto es usar la última estable verificada (`AGENTS.md`, sección 11).

## Analizadores y warnings como errores

### Tres fuentes de diagnósticos

| Prefijo | Quién los emite | Ejemplo |
|---------|-----------------|---------|
| `CS` | El compilador | `CS8625`: asignar `null` a un tipo que no admite nulos |
| `CA` | Analizadores de calidad de código del SDK | `CA1050`: declarar tipos dentro de un espacio de nombres |
| `IDE` | Analizadores de estilo | `IDE0161`: usar namespaces de ámbito de fichero |

El análisis de **calidad** (`CA`) viene activado por defecto para proyectos que apuntan a .NET 5 o posterior. El de **estilo** (`IDE`) está **desactivado** por defecto en las compilaciones de línea de comandos: hay que activarlo con `EnforceCodeStyleInBuild` y configurar en un `.editorconfig` cada regla que se quiera como aviso o error.

### Nuestra configuración

```xml
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>10.0-recommended</AnalysisLevel>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```

- **Nullable y warnings como errores**: cualquier aviso del compilador rompe el build. Es el control más barato para no acumular deuda.
- **`AnalysisLevel`**: el valor compuesto tiene la forma `<versión>-<modo>`. Su valor por defecto es `latest`, que incorpora las reglas nuevas de cada SDK que se instale; fijarlo a `10.0` evita que actualizar el SDK **añada reglas y rompa el build por sorpresa**. Los modos, de menos a más reglas, son `None`, `Default`, `Minimum`, `Recommended` y `All`. Usamos `recommended`.
- **`.editorconfig`**: las reglas de estilo con gravedad de error se declaran allí. Desde .NET 9, el formato de opción con gravedad (por ejemplo `csharp_style_namespace_declarations = file_scoped:error`) se respeta en el build.

### Qué se observó al provocar los fallos

| Cambio temporal | Resultado |
|-----------------|-----------|
| `string Value { get; set; } = null;` | Error `CS8625`; además `CA1050` (tipo fuera de un namespace) y `CA1805` (inicialización explícita al valor por defecto). El modo `recommended` es estricto |
| Namespace con llaves | Error `IDE0161`, con la sugerencia de pasar a ámbito de fichero |
| Namespace de ámbito de fichero y clase `sealed` | Compila sin avisos (control positivo) |

### Una excepción justificada

`AGENTS.md` fija para los tests el patrón de nombres `Metodo_Condicion_Resultado`, con guiones bajos. La regla `CA1707` (identificadores sin guiones bajos) lo prohibiría, así que el `.editorconfig` la desactiva **solo** para los ficheros de `tests/`. Cada supresión debe estar justificada así, en el propio fichero.

### Errores típicos

- Silenciar un aviso con `#pragma` o con una supresión global en lugar de entender la causa.
- Subir `AnalysisLevel` a `latest` sin querer y que cada actualización del SDK introduzca reglas nuevas.
- Creer que el análisis de estilo funciona en la línea de comandos sin `EnforceCodeStyleInBuild`.

## El formato de solución `.slnx`

Un fichero de solución en XML sencillo, que sustituye al formato `.sln` tradicional. Según la documentación:

- El SDK de .NET lo soporta desde la versión 9.0.200.
- En .NET 10, `dotnet new sln` genera `.slnx` **por defecto** (con `--format sln` se obtiene el formato antiguo).
- MSBuild lo admite desde Visual Studio 17.12. Si en una carpeta conviven un `.sln` y un `.slnx`, hay que indicar cuál se construye.
- `dotnet sln migrate` convierte un `.sln` existente.

El de este proyecto, `AgenticSandbox.slnx`, es legible a simple vista:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Sandbox.Agents/Sandbox.Agents.csproj" />
    <Project Path="src/Sandbox.Application/Sandbox.Application.csproj" />
    <Project Path="src/Sandbox.Domain/Sandbox.Domain.csproj" />
    <Project Path="src/Sandbox.Runner/Sandbox.Runner.csproj" />
  </Folder>
</Solution>
```

Las carpetas de solución (`/src/`, y `/tests/` cuando existan los proyectos de tests) agrupan proyectos en el explorador del IDE y no tienen por qué coincidir con carpetas de disco, aunque aquí sí lo hacen.

## Tests: xUnit v3 sobre Microsoft.Testing.Platform

### Qué usamos

`xunit.v3` (4.0.2, la última estable verificada el 2026-10-31) con **Microsoft.Testing.Platform** (MTP), el ejecutor de tests moderno de .NET. Frente al ejecutor clásico (VSTest), MTP es el ejecutor moderno; el SDK 10 lo soporta de forma nativa en `dotnet test`, aunque la plantilla `globaljson` sigue proponiendo VSTest por defecto.

### Lo que se descubrió en la práctica

Estas cuatro cosas se observaron al probar la solución (y antes, en un experimento desechable) y no son obvias:

1. **`dotnet test` falla sin configuración.** Con xUnit v3 en el SDK 10, sin más, termina con el error "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later". Hay que optar por el modo nuevo.
2. **La opción está en `global.json`**: `"test": { "runner": "Microsoft.Testing.Platform" }`. Es una propiedad disponible desde el SDK 10. La plantilla `dotnet new globaljson` tiene una opción `--test-runner` cuyo valor por defecto es VSTest.
3. **Los proyectos de tests son ejecutables** (`OutputType` `Exe`) y basta un único paquete, `xunit.v3`: no hicieron falta `Microsoft.NET.Test.Sdk` ni el runner de VSTest.
4. **xUnit v3 no añade `using Xunit` implícito**: sin `<Using Include="Xunit" />` en el `.csproj`, `[Fact]` no se reconoce (error `CS0246`).

Un quinto detalle afecta al diseño: con MTP, **un proyecto de tests sin ningún test hace fallar `dotnet test`** (código de salida 8, "No se ejecutaron pruebas"). Por eso cada proyecto de tests debe tener al menos un test desde el primer día.

### Tests de arquitectura: reglas de capas como tests

`LayerRulesTests` (en `Sandbox.Scenarios.Tests`) convierte la regla "el dominio y la aplicación no dependen de la IA" en algo que falla si se incumple. La técnica:

- `Assembly.Load("Sandbox.Domain")` carga el ensamblado, y `GetReferencedAssemblies()` lista los ensamblados que usa. El test recorre esas referencias de forma recursiva (sin entrar en el propio framework) y comprueba que ninguna empieza por un prefijo de librería de IA (`Microsoft.Extensions.AI`, `OpenAI`, `Azure.AI`, `Anthropic`, `ModelContextProtocol`).
- Se puede cargar un ensamblado que el código de test no usa porque la referencia de proyecto **copia su salida** junto a los tests.
- También comprueba la dirección de capas: la aplicación no referencia a `Agents` ni a `Runner`, y el dominio no referencia a ningún otro ensamblado `Sandbox.*`.

**Qué se observó al forzar el fallo**: se añadió temporalmente al dominio una referencia a `Microsoft.Extensions.AI.Abstractions` usada en código. El test falló y mostró `Collection: ["Microsoft.Extensions.AI.Abstractions"]`, es decir, identifica el ensamblado prohibido. Al revertirlo, volvió a pasar.

**Límite que conviene conocer**: el compilador omite las referencias que el código no usa, así que el test detecta dependencias **efectivas**. Una referencia declarada en el `.csproj` pero sin ningún uso en el código no aparece; para eso sirve `dotnet list <proyecto> reference`.

### Los tests de cableado son temporales

`Sandbox.Domain.Tests` y `Sandbox.Agents.Tests` tienen de momento un único test que solo comprueba que el ensamblado bajo prueba se carga con el nombre esperado. Existen para que el proyecto tenga al menos un test y para detectar una referencia de proyecto rota. Llevan un comentario que lo dice y **se sustituirán por tests reales** en las fases 3 (dominio) y 4 (tools).

### Una supresión justificada, comprobada

El `.editorconfig` desactiva `CA1707` solo para `tests/`. Se comprobó que es necesaria: al quitar esa sección, el test `Load_DomainAssembly_HasExpectedName` provoca el error `CA1707` y el build falla. Una supresión que no hace falta sería ruido; una que sí, debe quedar así de demostrada.

### Errores típicos

- Ejecutar `dotnet test` en el SDK 10 con xUnit v3 sin el bloque `test.runner` en `global.json`.
- Crear un proyecto de tests y dejarlo vacío mientras llega el primer test.
- Olvidar el `Using` de `Xunit` y perder tiempo con un error de compilación que parece de otra cosa.
- Escribir un test de arquitectura que compruebe una lista de nombres en lugar de las referencias reales: pasaría aunque el código estuviera mal.

## Saltos de línea: `.gitattributes`

> Añadido con el change `add-container-build`. **Verificado**: 2026-10-31 contra la documentación de Git ([gitattributes](https://git-scm.com/docs/gitattributes)) y con pruebas en este repositorio.

### Qué es

Windows termina las líneas con dos caracteres (CRLF, `\r\n`); Linux y macOS, con uno (LF, `\n`). Git puede convertir entre ambos al guardar un fichero (*checkin*) y al extraerlo (*checkout*). `.gitattributes` es el fichero versionado que dice **qué** conversión aplica a cada ruta, igual para todos los que clonan el repositorio.

### Por qué importa aquí

Lo que se ejecuta dentro de un contenedor Linux (scripts de shell y, en general, lo que interpreta Linux) falla o se comporta de forma rara con CRLF: el clásico `/bin/sh^M: bad interpreter`. Desde el change `add-container-build` el repositorio tiene `Dockerfile` y `compose.yaml`, y en Windows el contexto de build se envía **tal como está en el disco**. La configuración de Git de cada desarrollador (`core.autocrlf`) no debe decidir si eso funciona.

### Dos sitios distintos: índice y árbol de trabajo

```
   árbol de trabajo                    índice (repositorio)
  +----------------------+  checkin   +----------------------+
  | CRLF o LF, según     | ---------> | LF                   |
  | eol o core.autocrlf  | <--------- | (con text o          |
  |                      |  checkout  |  text=auto)          |
  +----------------------+            +----------------------+
```

- **`text` / `text=auto`** decide la normalización al guardar: con `text`, el índice guarda LF. Con `text=auto`, Git decide si el fichero es texto o binario, y si es texto lo normaliza, **salvo que ya estuviera en Git con CRLF** (documentación de Git).
- **`eol`** decide cómo queda el fichero en el árbol de trabajo al extraerlo. `eol=lf` deja en disco lo mismo que hay en el índice (LF). Declarar `eol` activa `text` por sí solo.
- Si una ruta no tiene `text` ni `eol` declarados, manda la configuración local (`core.autocrlf` o `core.eol`). Si los tiene, mandan los atributos.
- Si varias líneas coinciden con una ruta, **gana la última**, atributo por atributo.

### Nuestra configuración

```
* text=auto
Dockerfile text eol=lf
*.sh text eol=lf
*.yml text eol=lf
*.yaml text eol=lf
```

- `* text=auto`: el repositorio guarda LF en todos los ficheros de texto, aunque alguien no tenga `core.autocrlf` configurado.
- `eol=lf` solo donde Linux interpreta el fichero (`Dockerfile`, scripts y YAML). El resto (`.cs`, `.md`, `.csproj`...) se extrae como prefiera cada desarrollador.

**Por qué `.editorconfig` no fija `end_of_line`** (criterio propio): el editor y Git ya se ponen de acuerdo por separado. Si `.editorconfig` forzara LF en todo, en Windows con `core.autocrlf=true` el editor y Git estarían reescribiendo los saltos de línea en sentidos opuestos. El único sitio donde LF es obligatorio son los ficheros de la lista anterior, y ahí lo garantiza `.gitattributes`.

### Qué se observó en la práctica

- **Antes de añadirlo**, el índice ya estaba todo en LF (`git ls-files --eol` no mostró ningún `i/crlf`), y el árbol de trabajo de este equipo estaba en CRLF por `core.autocrlf=true`. Por eso `git add --renormalize .` no cambió ningún fichero versionado: no había nada que normalizar.
- `git check-attr eol -- Dockerfile compose.yaml x.sh` devuelve `lf` para los tres, y `unspecified` para un `.cs` o un `.json`.
- Un fichero nuevo con CRLF, añadido al índice, aparece como `i/lf w/crlf attr/text=auto`: se guarda con LF aunque el disco tenga CRLF.
- En un clon con `core.autocrlf=true`, los YAML pasaron de `w/crlf` a **`w/lf`** al aplicar el `.gitattributes`, y `README.md` o `Program.cs` siguieron en `w/crlf`.
- En un clon limpio con `core.autocrlf=true`, `Dockerfile` y `compose.yaml` salen con LF, y `.dockerignore` con **CRLF** (no está en la lista de `eol=lf`). Docker lo tolera: el contexto quedó igual de filtrado (10,25 kB) y el build sin caché pasó. No se ha comprobado con Podman. Como funciona, no se añadió a la lista "por si acaso"; si algún motor fallara con él, bastaría con añadir `.dockerignore text eol=lf`.
- **Un clon que ya existía no reescribe sus ficheros** solo por recibir el `.gitattributes`: la conversión ocurre al extraer. Para forzarla en un clon local se puede volver a extraer todo (`git rm --cached -r .` y `git reset --hard`, con el árbol de trabajo limpio). La documentación de Git describe la conversión "al guardar y al extraer", pero no dice explícitamente qué pasa con los ficheros ya extraídos: esto es lo observado aquí.

### Cómo comprobarlo

```bash
git ls-files --eol            # i/ = índice, w/ = árbol de trabajo, attr/ = atributos que aplican
git check-attr eol -- <ruta>  # qué valor de eol tiene una ruta
git add --renormalize .       # tras cambiar atributos: muestra en git status lo que se normalizaría
```

### Errores típicos

- Confiar en `core.autocrlf`: es configuración local, cada uno tiene la suya y no viaja con el repositorio.
- Añadir `* text=auto` a un repositorio que ya tiene ficheros con CRLF en el índice y no renormalizar: `text=auto` no los toca.
- Forzar `eol=lf` en todo y pelearse con editores de Windows que no lo respetan.
- Pensar que el `.gitattributes` corrige solo los ficheros ya extraídos.

## Globalización invariante

> Añadido con el change `add-container-build`. Decisión registrada en [ADR 0001](../adr/0001-globalizacion-invariante.md). **Verificado**: 2026-10-31 contra Microsoft Learn y el documento de diseño del runtime (enlaces en Referencias), y con tests en este repositorio.

### Qué es

La **globalización** es todo lo que depende de la cultura: cómo se escriben números y fechas, cómo se comparan y ordenan textos, mayúsculas y minúsculas, nombres de zonas horarias. .NET obtiene esos datos de **ICU** (International Components for Unicode), una biblioteca que en Linux viene del sistema operativo y en Windows también está integrada.

El **modo de globalización invariante** hace que la aplicación funcione **sin datos de cultura**: todo se comporta como la cultura invariante (similar al inglés, sin país), y .NET no necesita ICU. Se activa con la propiedad de MSBuild `InvariantGlobalization`, con `System.Globalization.Invariant` en `runtimeconfig.json` o con la variable `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`.

Según el documento de diseño del runtime, en modo invariante:

- los números y las fechas usan siempre el formato de la cultura invariante (el separador decimal es siempre `.`);
- `Compare`, `IndexOf` y `LastIndexOf` son **siempre ordinales**, no lingüísticas.

### `PredefinedCulturesOnly`: por qué `es-ES` lanza una excepción

Desde .NET 6, en modo invariante y por defecto, **no se puede crear ninguna cultura salvo la invariante** (`PredefinedCulturesOnly = true`, según Microsoft Learn). Así, `CultureInfo.GetCultureInfo("es-ES")` lanza `CultureNotFoundException` en lugar de devolver una "cultura española" con datos falsos. Es un fallo explícito, mejor que un formato engañoso. Al publicar el Runner, el `runtimeconfig.json` incluye las dos opciones:

```json
"System.Globalization.Invariant": true,
"System.Globalization.PredefinedCulturesOnly": true
```

### Por qué lo activamos

Las imágenes de contenedor *chiseled* de .NET no incluyen ICU, y su configuración define `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` (observado en la configuración de la imagen; Microsoft Learn explica que las imágenes optimizadas por tamaño solo funcionan con apps en modo invariante). Sin hacer nada, la solución se comportaría de tres formas: Windows con la cultura de la máquina, los tests en la imagen SDK con ICU de Ubuntu y la imagen final en modo invariante. Activándolo en `Directory.Build.props`, los tres entornos se comportan igual. Las alternativas descartadas (imagen `-extra` con ICU, o imagen sin *chiseled*) están en el ADR.

### Qué se observó en la práctica

En este equipo, cuya cultura es `es-ES`, los tests de `tests/Sandbox.Scenarios.Tests/GlobalizationTests.cs`:

| | Con `InvariantGlobalization` | Sin la propiedad |
|---|---|---|
| `1234.5.ToString("N2", CultureInfo.CurrentCulture)` | `1,234.50` | `1.234,50` |
| `CultureInfo.GetCultureInfo("es-ES")` | `CultureNotFoundException` | Devuelve la cultura, sin error |

Los dos tests fallan al quitar la propiedad: protegen la decisión.

### Qué implica para el proyecto

- El dominio de la fase 3 **no puede depender de la cultura de la máquina**. Importes, fechas y textos dirigidos al LLM se formatean de forma explícita (formatos fijos, ISO 8601).
- Las comparaciones de texto son ordinales. Si algún escenario necesitara reglas lingüísticas, habría que revisar el ADR con un change propio.
- La imagen *chiseled* tampoco trae tzdata; si alguna vez hacen falta zonas horarias con nombre, habrá que resolverlo aparte.

### Errores típicos

- Formatear con la cultura actual y que los tests pasen en una máquina y fallen en otra o en el contenedor.
- Usar una imagen *chiseled* sin activar el modo invariante en el proyecto: funciona, pero los tests (con ICU) prueban otro comportamiento.
- Esperar que `new CultureInfo("es-ES")` funcione "a medias" en modo invariante: con `PredefinedCulturesOnly`, lanza una excepción.

## Referencias

- Resumen de `global.json`: <https://learn.microsoft.com/dotnet/core/tools/global-json>
- Seleccionar la versión de .NET que se usa: <https://learn.microsoft.com/dotnet/core/versions/selection>
- Fijar versiones en una actualización de .NET: <https://learn.microsoft.com/dotnet/core/install/upgrade#version-pinning>
- Gestión central de paquetes: <https://learn.microsoft.com/nuget/consume-packages/central-package-management>
- Error `NU1008`: <https://learn.microsoft.com/nuget/reference/errors-and-warnings/nu1008>
- Análisis de código de .NET (visión general): <https://learn.microsoft.com/dotnet/fundamentals/code-analysis/overview>
- Propiedades de MSBuild para análisis (`AnalysisLevel`, `EnforceCodeStyleInBuild`): <https://learn.microsoft.com/dotnet/core/project-sdk/msbuild-props#code-analysis-properties>
- `dotnet new sln` genera `.slnx` por defecto: <https://learn.microsoft.com/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default>
- Comando `dotnet sln` (incluye `migrate`): <https://learn.microsoft.com/dotnet/core/tools/dotnet-sln>
- Plantillas de `dotnet new` (incluye `globaljson --test-runner`): <https://learn.microsoft.com/dotnet/core/tools/dotnet-new-sdk-templates>
- Documentación de xUnit v3 y de Microsoft.Testing.Platform: **no consultada directamente**; lo descrito sale de la documentación de `global.json` y de las pruebas realizadas en este repositorio.
- Comportamiento de `.gitignore` y de `git check-ignore`: documentación de Git, **no verificada** en esta sesión.
- Atributos de Git (`text`, `eol`, `--renormalize`, precedencia): <https://git-scm.com/docs/gitattributes> (consultado el 2026-10-31).
- Opciones de globalización del runtime (`InvariantGlobalization`, `PredefinedCulturesOnly`): <https://learn.microsoft.com/dotnet/core/runtime-config/globalization>
- Imágenes de contenedor de .NET (las optimizadas por tamaño no incluyen ICU): <https://learn.microsoft.com/dotnet/core/docker/container-images>
- Diseño del modo invariante (formato, comparaciones ordinales): <https://github.com/dotnet/runtime/blob/main/docs/design/features/globalization-invariant-mode.md>; el documento no menciona los cambios de versiones recientes, así que solo se citan de él el formato y las comparaciones.
