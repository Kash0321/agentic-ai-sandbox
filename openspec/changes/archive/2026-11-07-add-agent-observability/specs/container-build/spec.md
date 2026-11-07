## ADDED Requirements

### Requirement: Panel de trazas opcional en compose
El repositorio SHALL definir con compose un servicio `dashboard`, solo en el perfil `dashboard`, con un panel que recibe OTLP. Su imagen SHALL fijarse con etiqueta de versión y digest. SHALL ejecutarse sin usuario root, con sistema de ficheros de solo lectura (salvo un directorio temporal en memoria), sin capacidades, sin escalar privilegios y con límites de memoria y CPU, y publicar sus puertos solo en `127.0.0.1`.

#### Scenario: Sin perfil no hay panel
- **GIVEN** el repositorio
- **WHEN** se ejecuta `docker compose run --rm runner` sin activar el perfil `dashboard`
- **THEN** no se crea ningún contenedor del panel

#### Scenario: Panel endurecido en marcha
- **GIVEN** el perfil `dashboard` activado
- **WHEN** se arranca el servicio `dashboard` y se inspecciona su contenedor
- **THEN** está en ejecución con un usuario distinto de root, sistema de ficheros raíz de solo lectura, todas las capacidades eliminadas, `no-new-privileges` activo, límites de memoria y CPU, y sus puertos publicados solo en `127.0.0.1`

#### Scenario: Imagen del panel fijada
- **GIVEN** la definición de compose
- **WHEN** se revisa la imagen del servicio `dashboard`
- **THEN** incluye una etiqueta de versión concreta y un digest `sha256`, y no usa `latest`

### Requirement: Endpoint de telemetría del runner vacío por defecto
El servicio `runner` SHALL recibir `OTEL_EXPORTER_OTLP_ENDPOINT` desde la variable de entorno del mismo nombre en el anfitrión, vacía si no está definida. Así, la telemetría solo se exporta cuando quien ejecuta lo pide.

#### Scenario: Ejecución sin variable
- **GIVEN** el anfitrión sin `OTEL_EXPORTER_OTLP_ENDPOINT` definida
- **WHEN** se ejecuta `docker compose run --rm runner`
- **THEN** el Runner arranca sin exportación de telemetría

#### Scenario: Ejecución hacia el panel
- **GIVEN** el servicio `dashboard` en marcha y `OTEL_EXPORTER_OTLP_ENDPOINT` igual a `http://dashboard:18889` en el anfitrión
- **WHEN** se ejecuta `docker compose run --rm runner --scenario happy-order` con una API key válida
- **THEN** el Runner termina con código 0 y el panel muestra la traza de la ejecución
