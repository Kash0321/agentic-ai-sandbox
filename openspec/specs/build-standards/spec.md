# build-standards Specification

## Purpose
Define los estándares de build que toda la solución cumple: SDK fijado, calidad estricta del compilador, versiones de paquetes centralizadas, convenciones comprobadas por el build e higiene del repositorio que protege los secretos. Garantiza que cualquiera obtiene el mismo resultado y que los errores se detectan al compilar.

## Requirements

### Requirement: SDK de .NET fijado por el repositorio
El repositorio SHALL declarar la versión del SDK de .NET con la que se construye la solución. Si el SDK instalado queda fuera del rango permitido, el build SHALL fallar con un error explícito, sin usar silenciosamente otra versión.

#### Scenario: Build con el SDK declarado
- **GIVEN** un equipo con el SDK declarado por el repositorio instalado
- **WHEN** se compila la solución
- **THEN** el build usa ese SDK y termina correctamente

#### Scenario: Build con un SDK fuera del rango
- **GIVEN** un equipo cuyo SDK es anterior al mínimo declarado
- **WHEN** se intenta compilar la solución
- **THEN** el build falla indicando que no se encuentra un SDK compatible

### Requirement: Nullable activado y warnings como errores
Todos los proyectos de la solución, incluidos los de tests, SHALL compilar con los tipos de referencia anulables habilitados y SHALL tratar cualquier advertencia del compilador como error.

#### Scenario: Una advertencia de nulabilidad rompe el build
- **GIVEN** un proyecto de la solución con una asignación de `null` a un tipo de referencia no anulable
- **WHEN** se compila la solución
- **THEN** el build falla con un error de compilador y no solo con una advertencia

#### Scenario: La solución vacía compila sin advertencias
- **GIVEN** un clon limpio del repositorio
- **WHEN** se compila la solución completa
- **THEN** el build termina con cero advertencias y cero errores

### Requirement: Versiones de paquetes centralizadas
Las versiones de todos los paquetes NuGet SHALL declararse en un único fichero central. Los ficheros de proyecto SHALL NOT especificar versiones de paquetes.

#### Scenario: Un proyecto con versión de paquete inline falla
- **GIVEN** un proyecto cuyo `PackageReference` incluye una versión explícita
- **WHEN** se restaura o compila la solución
- **THEN** la operación falla indicando que la versión debe definirse de forma central

#### Scenario: Una versión se cambia en un solo sitio
- **GIVEN** un paquete usado por varios proyectos de la solución
- **WHEN** se modifica su versión en el fichero central
- **THEN** todos los proyectos que lo usan adoptan la nueva versión sin tocar sus ficheros de proyecto

### Requirement: Convenciones comprobadas por el build
Las convenciones de código que un analizador pueda comprobar SHALL hacerse cumplir en el build como errores. En concreto, los namespaces SHALL declararse con ámbito de fichero.

#### Scenario: Un namespace con llaves rompe el build
- **GIVEN** un fichero de código con un namespace declarado con bloque de llaves
- **WHEN** se compila la solución
- **THEN** el build falla señalando la convención incumplida

### Requirement: Build y tests sin red ni secretos
Una vez restaurados los paquetes, compilar y ejecutar los tests SHALL ser posible sin acceso a red y sin claves de API ni otros secretos.

#### Scenario: Tests en un entorno aislado
- **GIVEN** paquetes ya restaurados y un entorno sin red ni variables de secretos
- **WHEN** se compila y se ejecutan los tests de la solución
- **THEN** el build y los tests terminan correctamente

### Requirement: Higiene del repositorio y protección de secretos
El repositorio SHALL ignorar en Git los artefactos de compilación y los ficheros locales o con secretos: como mínimo `bin/`, `obj/`, `.vs/`, ficheros `*.user`, la carpeta `secrets/` y los ficheros `.env`. La plantilla `.env.example` SHALL seguir siendo versionable.

#### Scenario: La compilación no ensucia el árbol de Git
- **GIVEN** un clon limpio del repositorio
- **WHEN** se compila la solución y se consulta el estado de Git
- **THEN** no aparecen artefactos de compilación como ficheros sin seguimiento

#### Scenario: Los secretos locales quedan ignorados
- **GIVEN** un fichero `.env` y un fichero dentro de `secrets/` creados en la raíz del repositorio
- **WHEN** se consulta el estado de Git
- **THEN** ambos aparecen como ignorados y no como ficheros candidatos a versionar

#### Scenario: La plantilla de entorno sigue versionable
- **GIVEN** un fichero `.env.example` en la raíz del repositorio
- **WHEN** se consulta el estado de Git
- **THEN** el fichero no está ignorado

### Requirement: Globalización invariante en toda la solución
Todos los proyectos de la solución, incluidos los de tests, SHALL ejecutarse en modo de globalización invariante, de modo que el comportamiento sea el mismo en local, en los tests y en el contenedor, sea cual sea la cultura de la máquina. Pedir una cultura concreta MUST fallar de forma explícita en lugar de usar datos del sistema operativo.

#### Scenario: Formato independiente de la máquina
- **GIVEN** una máquina cuya cultura del sistema es `es-ES`
- **WHEN** un test formatea el número `1234.5` con el formato `N2` y la cultura actual
- **THEN** el resultado es `1,234.50`, el de la cultura invariante

#### Scenario: Una cultura concreta no está disponible
- **GIVEN** cualquier proyecto de la solución en ejecución
- **WHEN** se pide la cultura `es-ES`
- **THEN** la operación falla con un error de cultura no encontrada

#### Scenario: La configuración llega a la aplicación publicada
- **GIVEN** el Runner publicado
- **WHEN** se inspecciona su configuración de runtime
- **THEN** el modo de globalización invariante está activado

### Requirement: Saltos de línea normalizados
El repositorio SHALL guardar los ficheros de texto con saltos de línea LF. Los ficheros que se interpretan dentro de contenedores Linux (definición de imagen, scripts de shell y YAML) SHALL extraerse con LF en el árbol de trabajo, sea cual sea la configuración de Git del desarrollador.

#### Scenario: Extracción con conversión automática activada
- **GIVEN** un clon con `core.autocrlf=true`
- **WHEN** se extraen la definición de imagen y los ficheros YAML
- **THEN** en el árbol de trabajo tienen saltos de línea LF

#### Scenario: Un fichero con CRLF se guarda con LF
- **GIVEN** un fichero de texto nuevo con saltos de línea CRLF
- **WHEN** se añade al índice de Git
- **THEN** el índice lo guarda con LF
