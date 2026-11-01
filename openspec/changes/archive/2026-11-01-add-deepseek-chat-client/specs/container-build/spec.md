# Spec Delta

## MODIFIED Requirements

### Requirement: Imagen final mínima y no root
La imagen final SHALL basarse en una imagen de runtime, sin el SDK, y SHALL contener solo la aplicación publicada del Runner. SHALL ejecutarse con un usuario no root y su punto de entrada SHALL ser el Runner.

#### Scenario: Usuario no root
- **GIVEN** la imagen final construida
- **WHEN** se inspecciona su usuario configurado
- **THEN** es un UID distinto de 0

#### Scenario: Solo la aplicación publicada
- **GIVEN** la imagen final construida
- **WHEN** se lista el contenido de su directorio de aplicación
- **THEN** contiene los ensamblados publicados del Runner y sus dependencias, y no contiene código fuente, proyectos de tests ni el SDK

#### Scenario: El Runner arranca
- **GIVEN** la imagen final construida y ninguna API key disponible
- **WHEN** se ejecuta un contenedor sin argumentos
- **THEN** el Runner arranca, termina con el código de salida de configuración inválida (2) e indica qué opción falta

### Requirement: Ejecución endurecida del Runner
El servicio `runner` SHALL ejecutarse con el sistema de ficheros raíz de solo lectura, sin capacidades del kernel, sin posibilidad de escalar privilegios y con límites de memoria y CPU.

#### Scenario: Restricciones aplicadas
- **GIVEN** el servicio `runner` en ejecución o recién creado
- **WHEN** se inspecciona la configuración del contenedor
- **THEN** el sistema de ficheros raíz es de solo lectura, se han eliminado todas las capacidades, `no-new-privileges` está activo y hay límites de memoria y CPU

#### Scenario: El Runner funciona con las restricciones
- **GIVEN** las restricciones anteriores y una API key válida como fichero de secreto
- **WHEN** se ejecuta `docker compose run --rm runner`
- **THEN** el Runner termina con código de salida 0 y sin errores de escritura en disco

## REMOVED Requirements

### Requirement: Runner sin red ni secretos declarados
**Reason**: Desde la fase 2 el Runner llama a la API de DeepSeek y necesita red y la API key.
**Migration**: Lo sustituye el requisito "Runner con red y la API key como secreto por fichero". El control de salida a un único destino se especificará en la fase 5.

## ADDED Requirements

### Requirement: Runner con red y la API key como secreto por fichero
El servicio `runner` SHALL tener acceso a red para llegar al proveedor del modelo y SHALL recibir la API key como secreto de compose montado en un fichero, cuya ruta en el anfitrión se puede elegir con una variable de entorno y que nunca está versionado. La clave MUST NOT pasarse por variable de entorno en compose ni quedar en la imagen.

#### Scenario: Secreto montado
- **GIVEN** un fichero con la API key fuera del repositorio, indicado con la variable de entorno prevista
- **WHEN** se ejecuta `docker compose run --rm runner`
- **THEN** el contenedor ve la clave como un fichero en el directorio de secretos y el Runner la usa

#### Scenario: La clave no aparece en la configuración del contenedor
- **GIVEN** el servicio `runner` ejecutado con la clave como secreto
- **WHEN** se inspeccionan sus variables de entorno y su configuración con `docker inspect`
- **THEN** la clave no aparece

#### Scenario: Fichero de secreto ausente
- **GIVEN** la variable de entorno apuntando a un fichero que no existe
- **WHEN** se ejecuta `docker compose run --rm runner`
- **THEN** el Runner termina con el código de configuración inválida (2) e indica qué opción falta
