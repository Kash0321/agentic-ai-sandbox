## MODIFIED Requirements

### Requirement: Actualizaciones de versión periódicas
El repositorio SHALL configurar actualizaciones automáticas de versión para los paquetes NuGet, las imágenes base de los `Dockerfile` del repositorio, las imágenes de los servicios de compose y las acciones de GitHub, con frecuencia semanal. Cada actualización SHALL llegar como pull request contra `main` y pasar los mismos checks obligatorios que cualquier otro cambio.

#### Scenario: Nueva versión disponible
- **GIVEN** una dependencia fijada para la que se publica una versión estable más reciente
- **WHEN** se ejecuta la revisión semanal tras el periodo de espera configurado
- **THEN** se abre un pull request que la actualiza y la CI lo verifica

#### Scenario: Configuración válida
- **GIVEN** la configuración de actualizaciones del repositorio
- **WHEN** GitHub la procesa
- **THEN** no informa de ningún error de configuración para ninguno de los cuatro ecosistemas

#### Scenario: Imagen de un servicio de compose
- **GIVEN** la imagen del panel de trazas fijada en compose con etiqueta y digest
- **WHEN** se publica una versión estable más reciente y se ejecuta la revisión semanal
- **THEN** se abre un pull request que la actualiza y la referencia sigue incluyendo una etiqueta concreta y un digest `sha256`
