# container-build Specification

## Purpose
Define cómo se construye, se prueba y se ejecuta la solución en contenedor de forma reproducible y segura: una imagen por etapas con un objetivo de tests sin red, imágenes base fijadas, un contexto de build sin secretos, una imagen final mínima y no root, y una ejecución local endurecida del Runner.

## Requirements

### Requirement: Objetivo de tests en contenedor
El repositorio SHALL proporcionar una definición de imagen por etapas que restaura, compila y ejecuta todos los tests de la solución en una etapa que se puede construir por separado. Si algún test falla, la construcción de esa etapa MUST fallar.

#### Scenario: Tests en verde
- **GIVEN** un clon del repositorio con todos los tests en verde
- **WHEN** se construye la etapa de tests (`docker build --target test .`)
- **THEN** la construcción termina correctamente tras ejecutar los tests de los tres proyectos de tests

#### Scenario: Un test en rojo rompe la construcción
- **GIVEN** un test modificado para que falle
- **WHEN** se construye la etapa de tests
- **THEN** la construcción falla y el resultado identifica el test fallido

### Requirement: Tests en contenedor sin red
La ejecución de los tests dentro del contenedor SHALL hacerse sin acceso a red. Solo la restauración de paquetes MAY usar la red.

#### Scenario: Los tests pasan sin red
- **GIVEN** los paquetes ya restaurados en una etapa anterior
- **WHEN** se ejecuta la etapa de tests
- **THEN** los tests se ejecutan con la red deshabilitada y pasan

#### Scenario: La etapa de tests no resuelve nombres externos
- **GIVEN** la misma configuración de red que la etapa de tests
- **WHEN** un comando intenta resolver `api.nuget.org`
- **THEN** la resolución falla

### Requirement: Imágenes base fijadas
Cada imagen base SHALL referenciarse con una etiqueta de versión explícita y con su digest inmutable. La definición MUST NOT usar `latest` ni etiquetas flotantes sin digest. La imagen del SDK SHALL cumplir la versión mínima de SDK que declara el repositorio.

#### Scenario: Toda imagen base está fijada
- **GIVEN** la definición de imagen del repositorio
- **WHEN** se revisan sus imágenes base
- **THEN** cada una incluye una etiqueta de versión concreta y un digest `sha256`, y ninguna usa `latest`

#### Scenario: El SDK de la imagen satisface el repositorio
- **GIVEN** la versión mínima de SDK declarada por el repositorio
- **WHEN** se construye la etapa de tests
- **THEN** la compilación no falla por incompatibilidad de SDK

### Requirement: Contexto de build mínimo y sin secretos
El contexto que se envía al motor de contenedores SHALL incluir solo los ficheros necesarios para construir la solución. MUST excluir los artefactos de compilación locales, los metadatos de Git, la carpeta `secrets/` y los ficheros `.env`.

#### Scenario: Los secretos locales no llegan a ninguna imagen
- **GIVEN** un fichero dentro de `secrets/` y un fichero `.env` con contenido conocido en la raíz del repositorio
- **WHEN** se construyen la etapa de tests y la imagen final
- **THEN** ni esos ficheros ni su contenido aparecen en ninguna de las dos imágenes

#### Scenario: Los artefactos locales no entran en el build
- **GIVEN** carpetas `bin/` y `obj/` generadas por un build local
- **WHEN** se construye la imagen
- **THEN** el contexto enviado no las incluye y la compilación dentro del contenedor no reutiliza artefactos del anfitrión

### Requirement: Configuración de imagen sin secretos
La definición de imagen SHALL NOT declarar secretos ni claves en argumentos de construcción, variables de entorno ni instrucciones que queden en el historial de la imagen.

#### Scenario: Historial y entorno limpios
- **GIVEN** la imagen final construida
- **WHEN** se inspeccionan sus variables de entorno y su historial
- **THEN** no aparece ninguna clave, contraseña ni variable de secretos, y las únicas variables son las heredadas de la imagen base de runtime

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
- **GIVEN** la imagen final construida
- **WHEN** se ejecuta un contenedor sin argumentos
- **THEN** el Runner termina con código de salida 0

### Requirement: Restauración cacheable
Un cambio solo en el código fuente SHALL NOT invalidar el paso de restauración de paquetes. La restauración SHALL depender solo de los ficheros que definen dependencias y configuración de build.

#### Scenario: Cambiar código reutiliza la restauración
- **GIVEN** una construcción previa de la etapa de tests
- **WHEN** se modifica un fichero `.cs` y se vuelve a construir la etapa de tests
- **THEN** el paso de restauración se reutiliza de la caché y solo se repiten la compilación y los tests

#### Scenario: Olvidar un proyecto falla de forma explícita
- **GIVEN** un proyecto nuevo añadido a la solución pero no incluido en la etapa de restauración
- **WHEN** se construye la imagen
- **THEN** la restauración falla indicando el proyecto que falta, en lugar de compilar una solución incompleta

### Requirement: Ejecución one-shot del Runner con compose
El repositorio SHALL definir con compose un servicio `runner` que ejecuta el Runner una vez a partir de la imagen final, propaga su código de salida y se puede eliminar al terminar.

#### Scenario: Ejecución y limpieza
- **GIVEN** el repositorio con la imagen construible
- **WHEN** se ejecuta `docker compose run --rm runner`
- **THEN** el Runner se ejecuta, el comando devuelve su código de salida y el contenedor no queda creado al terminar

### Requirement: Ejecución endurecida del Runner
El servicio `runner` SHALL ejecutarse con el sistema de ficheros raíz de solo lectura, sin capacidades del kernel, sin posibilidad de escalar privilegios y con límites de memoria y CPU.

#### Scenario: Restricciones aplicadas
- **GIVEN** el servicio `runner` en ejecución o recién creado
- **WHEN** se inspecciona la configuración del contenedor
- **THEN** el sistema de ficheros raíz es de solo lectura, se han eliminado todas las capacidades, `no-new-privileges` está activo y hay límites de memoria y CPU

#### Scenario: El Runner funciona con las restricciones
- **GIVEN** las restricciones anteriores
- **WHEN** se ejecuta `docker compose run --rm runner`
- **THEN** el Runner termina con código de salida 0 y sin errores de escritura en disco

### Requirement: Runner sin red ni secretos declarados
Mientras el Runner no necesite servicios externos, el servicio `runner` SHALL ejecutarse sin acceso a red y SHALL NOT declarar secretos.

#### Scenario: Sin red
- **GIVEN** el servicio `runner`
- **WHEN** se inspecciona su configuración de red
- **THEN** no tiene ninguna red conectada salvo la interfaz local

#### Scenario: Sin secretos
- **GIVEN** la definición de compose
- **WHEN** se buscan secretos declarados para el servicio `runner`
- **THEN** no hay ninguno
