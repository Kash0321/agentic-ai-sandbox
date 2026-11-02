# runner-host Specification

## Purpose
Define cómo arranca y termina el Runner: de dónde lee su configuración y con qué prioridad, incluidos los secretos montados como ficheros; qué hace en su ejecución de comprobación; y qué código de salida devuelve en cada caso.

## Requirements

### Requirement: Fuentes de configuración con prioridad definida
El Runner SHALL leer la configuración, de menor a mayor prioridad, de: el fichero de configuración de la aplicación y su variante por entorno, los User Secrets (solo en el entorno `Development`), las variables de entorno, los argumentos de la línea de comandos y los ficheros de un directorio de secretos montado. Una fuente posterior MUST sobrescribir a las anteriores. El directorio de secretos MAY no existir.

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

### Requirement: Servicios de aplicación registrados al arrancar
El Runner SHALL registrar las operaciones de aplicación sobre el catálogo semilla, con el inyector de fallos configurado desde la sección `FaultInjection`. Sin esa sección, SHALL arrancar sin fallos inducidos. Una configuración del inyector inválida MUST detectarse al arrancar y terminar con el código 2, con un mensaje que nombre el ajuste inválido.

#### Scenario: Sin sección del inyector
- **GIVEN** una configuración válida de DeepSeek y ninguna sección `FaultInjection`
- **WHEN** se ejecuta el Runner con el cliente falso
- **THEN** termina con código 0 como en el saludo de comprobación

#### Scenario: Configuración del inyector inválida
- **GIVEN** una configuración válida de DeepSeek y una regla del inyector con probabilidad 1,5
- **WHEN** se ejecuta el Runner
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra el ajuste de probabilidad de esa regla

#### Scenario: Varias secciones inválidas a la vez
- **GIVEN** ninguna API key y una regla del inyector con una operación desconocida
- **WHEN** se ejecuta el Runner
- **THEN** termina con código 2, y el mensaje nombra la opción de la clave y el ajuste de operación de esa regla

#### Scenario: Operaciones disponibles en el contenedor de servicios
- **GIVEN** el host del Runner construido con la configuración por defecto
- **WHEN** se piden al contenedor de servicios los commands y las queries
- **THEN** se obtienen, y una query del cliente bloqueado del catálogo semilla devuelve su vista

### Requirement: Ejecución del agente desde la línea de comandos
Con `--scenario <id>`, el Runner SHALL ejecutar el agente con ese escenario de `sales-agent`. Con `--prompt "<texto>"`, SHALL ejecutarlo con ese texto como petición del cliente. El cliente de sesión SHALL ser el indicado con `--customer`; si no se indica, el del escenario; y si tampoco lo hay, `CUST-001`. Sin `--scenario` ni `--prompt`, el comportamiento SHALL ser el saludo de comprobación.

#### Scenario: Texto libre con otro cliente
- **GIVEN** el Runner con un cliente de chat falso que pide `get_customer` y luego responde
- **WHEN** se ejecuta con `--prompt "¿quién soy?" --customer CUST-002`
- **THEN** la tool devuelve el cliente `CUST-002` y el código de salida es 0

#### Scenario: Error del proveedor durante el agente
- **GIVEN** un cliente de chat que falla como un error de autenticación del proveedor
- **WHEN** se ejecuta con `--scenario happy-order`
- **THEN** termina con código 3, igual que en el saludo

### Requirement: Salida de una ejecución del agente
Tras ejecutar el agente, el Runner SHALL escribir por la salida estándar la respuesta final y un resumen con el número de llamadas al modelo, las tools invocadas con su resultado y los tokens de entrada y de salida. Los registros SHALL seguir yendo a la salida de errores.

#### Scenario: Escenario feliz con cliente falso
- **GIVEN** el Runner con un cliente de chat falso que pide `list_products`, después `place_order` y luego responde "Pedido ORD-0001 creado"
- **WHEN** se ejecuta con `--scenario happy-order`
- **THEN** termina con código 0, y la salida estándar muestra "Pedido ORD-0001 creado", 3 llamadas al modelo, las dos tools con éxito y los tokens

### Requirement: Argumentos de ejecución inválidos
Un escenario desconocido, `--scenario` y `--prompt` a la vez, o un texto vacío o de solo espacios MUST detectarse al arrancar, sin llamar al modelo, y terminar con el código 2 y un mensaje que nombre la opción.

#### Scenario: Escenario desconocido
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con `--scenario no-existe`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra la opción de escenario

#### Scenario: Escenario y texto a la vez
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con `--scenario happy-order --prompt "hola"`
- **THEN** termina con código 2 sin llamar al modelo

#### Scenario: Texto vacío
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con `--prompt "   "`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra la opción de texto
