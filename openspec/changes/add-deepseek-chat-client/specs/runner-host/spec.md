# Spec Delta

## Purpose

Define cómo arranca y termina el Runner: de dónde lee su configuración y con qué prioridad, incluidos los secretos montados como ficheros; qué hace en su ejecución de comprobación; y qué código de salida devuelve en cada caso.

## ADDED Requirements

### Requirement: Fuentes de configuración con prioridad definida
El Runner SHALL leer la configuración, de menor a mayor prioridad, de: el fichero de configuración de la aplicación, los User Secrets (solo en el entorno `Development`), las variables de entorno y los ficheros de un directorio de secretos montado. Una fuente posterior MUST sobrescribir a las anteriores. El directorio de secretos MAY no existir.

#### Scenario: Clave como fichero de secreto
- **GIVEN** un directorio de secretos con un fichero llamado `DeepSeek__ApiKey` que contiene una clave
- **WHEN** arranca el Runner
- **THEN** la opción de la API key toma el contenido de ese fichero

#### Scenario: El fichero de secreto prevalece sobre la variable de entorno
- **GIVEN** la API key definida a la vez como variable de entorno y como fichero de secreto, con valores distintos
- **WHEN** arranca el Runner
- **THEN** se usa el valor del fichero de secreto

#### Scenario: Sin directorio de secretos
- **GIVEN** un entorno sin directorio de secretos y con la clave en otra fuente
- **WHEN** arranca el Runner
- **THEN** arranca sin error y usa la clave de esa otra fuente

#### Scenario: User Secrets fuera de Development
- **GIVEN** la clave guardada solo en User Secrets y el entorno `Production`
- **WHEN** arranca el Runner
- **THEN** la clave no se encuentra y el arranque falla por configuración inválida

### Requirement: Saludo de comprobación
Al ejecutarse sin argumentos, el Runner SHALL enviar un mensaje de saludo fijo al modelo, mostrar el texto de la respuesta, el modelo que respondió y los tokens usados, y terminar.

#### Scenario: Saludo con cliente falso
- **GIVEN** el Runner configurado con un cliente falso que responde "hola"
- **WHEN** se ejecuta sin argumentos
- **THEN** la salida muestra "hola", el modelo y los tokens, y el código de salida es 0

#### Scenario: Saludo real en local
- **GIVEN** una API key válida en User Secrets y el entorno `Development`
- **WHEN** se ejecuta el Runner con `dotnet run`
- **THEN** la salida muestra la respuesta de DeepSeek y el código de salida es 0

### Requirement: Códigos de salida
El Runner SHALL terminar con un código de salida distinto para cada resultado:

- `0`: éxito;
- `2`: configuración inválida;
- `3`: fallo al llamar al modelo (autenticación, red o respuesta de error del proveedor);
- `1`: error inesperado.

En los casos distintos de 0 MUST mostrar un mensaje breve que identifique la causa, sin trazas internas ni secretos.

#### Scenario: Sin clave
- **GIVEN** ninguna fuente de configuración con API key
- **WHEN** se ejecuta el Runner
- **THEN** termina con código 2 y el mensaje indica qué opción falta

#### Scenario: Clave rechazada por el proveedor
- **GIVEN** una API key que el proveedor rechaza
- **WHEN** se ejecuta el Runner
- **THEN** termina con código 3 y el mensaje indica que la autenticación con el proveedor falló, sin fragmentos de la clave

#### Scenario: Error inesperado
- **GIVEN** un fallo no previsto durante la ejecución
- **WHEN** se produce
- **THEN** el Runner termina con código 1, el mensaje no incluye la traza y el detalle queda en el registro de errores
