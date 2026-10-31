# dependency-updates Specification

## Purpose
Define cómo se detectan y se proponen las actualizaciones de las dependencias fijadas del repositorio: paquetes NuGet, imágenes base y acciones de GitHub. El objetivo es que fijar versiones no implique quedarse con versiones vulnerables o desfasadas sin saberlo.

## Requirements

### Requirement: Actualizaciones de versión periódicas
El repositorio SHALL configurar actualizaciones automáticas de versión para los paquetes NuGet, las imágenes base de los `Dockerfile` del repositorio y las acciones de GitHub, con frecuencia semanal. Cada actualización SHALL llegar como pull request contra `main` y pasar los mismos checks obligatorios que cualquier otro cambio.

#### Scenario: Nueva versión disponible
- **GIVEN** una dependencia fijada para la que se publica una versión estable más reciente
- **WHEN** se ejecuta la revisión semanal tras el periodo de espera configurado
- **THEN** se abre un pull request que la actualiza y la CI lo verifica

#### Scenario: Configuración válida
- **GIVEN** la configuración de actualizaciones del repositorio
- **WHEN** GitHub la procesa
- **THEN** no informa de ningún error de configuración para ninguno de los tres ecosistemas

### Requirement: Actualizaciones agrupadas por ecosistema
Las actualizaciones de versión de un mismo ecosistema SHALL agruparse en un único pull request por ejecución, para limitar el número de pull requests que hay que revisar.

#### Scenario: Varias acciones desfasadas
- **GIVEN** dos o más acciones de GitHub con versiones nuevas en la misma semana
- **WHEN** se ejecuta la revisión semanal
- **THEN** se abre un único pull request que las actualiza todas

### Requirement: Las versiones siguen fijadas tras actualizar
Una actualización SHALL conservar la forma de fijación de cada dependencia: las imágenes base siguen con etiqueta y digest, las acciones siguen fijadas por SHA con su comentario de versión, y los paquetes NuGet siguen con versión exacta en el fichero central.

#### Scenario: Actualización de una acción
- **GIVEN** una acción fijada por SHA con un comentario de versión
- **WHEN** llega su pull request de actualización
- **THEN** la referencia usa el SHA de la nueva versión y el comentario indica esa versión

#### Scenario: Actualización de una imagen base
- **GIVEN** una imagen base fijada por etiqueta y digest
- **WHEN** llega su pull request de actualización
- **THEN** la referencia sigue incluyendo una etiqueta concreta y un digest `sha256`

### Requirement: Alertas y actualizaciones de seguridad
El repositorio SHALL tener activadas las alertas de vulnerabilidades de dependencias y las actualizaciones automáticas de seguridad. Estas SHALL proponerse sin esperar a la revisión semanal.

#### Scenario: Vulnerabilidad publicada en una dependencia
- **GIVEN** una dependencia del repositorio con una vulnerabilidad publicada que tiene versión corregida
- **WHEN** GitHub detecta la vulnerabilidad
- **THEN** aparece una alerta en el repositorio y se abre un pull request de seguridad con la versión corregida
