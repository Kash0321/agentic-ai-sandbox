# Spec Delta

## ADDED Requirements

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
