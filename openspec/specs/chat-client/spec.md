# chat-client Specification

## Purpose
Define cómo se conecta la solución a un modelo de lenguaje: opciones de conexión validadas, el modelo y el modo de razonamiento configurables, el cliente hacia DeepSeek, un cliente falso para probar sin red, y cómo se registran las llamadas y se tratan los errores sin exponer la clave.

## Requirements

### Requirement: Opciones de conexión validadas al arrancar
La solución SHALL leer de la configuración el endpoint, el modelo, el modo de razonamiento y la API key del proveedor, y SHALL validarlos al arrancar, antes de hacer ninguna llamada. Una opción ausente o inválida MUST impedir el arranque con un error que nombra la opción y no muestra su valor.

#### Scenario: Falta la API key
- **GIVEN** una configuración sin API key
- **WHEN** arranca el Runner
- **THEN** el arranque falla antes de llamar al modelo, el error nombra la opción de la API key y no se hace ninguna petición de red

#### Scenario: Endpoint inválido
- **GIVEN** una configuración cuyo endpoint no es una URL absoluta HTTPS
- **WHEN** arranca el Runner
- **THEN** el arranque falla y el error nombra la opción del endpoint

#### Scenario: Opciones válidas
- **GIVEN** una configuración con endpoint, modelo y API key válidos
- **WHEN** arranca el Runner
- **THEN** la validación pasa y el cliente queda disponible por inyección de dependencias

### Requirement: Modelo y razonamiento configurables con valores por defecto
El modelo SHALL ser configurable y, por defecto, SHALL ser `deepseek-flash`. El razonamiento del modelo (*thinking*) SHALL ser configurable y, por defecto, SHALL estar desactivado. La petición enviada al proveedor MUST reflejar el modo configurado.

#### Scenario: Valores por defecto
- **GIVEN** una configuración que solo indica la API key
- **WHEN** se envía un mensaje al modelo
- **THEN** la petición usa el modelo `deepseek-flash` y pide el razonamiento desactivado

#### Scenario: Razonamiento activado por configuración
- **GIVEN** una configuración con el razonamiento activado
- **WHEN** se envía un mensaje al modelo
- **THEN** la petición no desactiva el razonamiento y, si el proveedor lo devuelve, la respuesta lo incluye separado del texto

### Requirement: Cliente de chat hacia el proveedor compatible con OpenAI
La solución SHALL registrar un `IChatClient` que envía las peticiones al endpoint configurado con el formato de la API de OpenAI y autentica con la API key configurada. El código de dominio y de aplicación MUST NOT depender de él.

#### Scenario: Saludo contra el proveedor real
- **GIVEN** una API key válida de DeepSeek y acceso a red
- **WHEN** se envía un mensaje de saludo
- **THEN** se recibe una respuesta de texto no vacía, con el identificador del modelo que respondió y el número de tokens usados

#### Scenario: Capas respetadas
- **GIVEN** la solución compilada
- **WHEN** se ejecutan las pruebas de reglas de capas
- **THEN** ni el dominio ni la aplicación referencian librerías de IA

### Requirement: Cliente falso para pruebas sin red
La solución SHALL ofrecer a los tests un cliente de chat falso, determinista y sin red, que se puede usar en lugar del real sin cambiar el código que lo consume. Los tests MUST NOT necesitar red ni API key.

#### Scenario: Respuesta programada
- **GIVEN** un cliente falso programado para responder un texto concreto
- **WHEN** el código bajo prueba envía un mensaje
- **THEN** recibe exactamente ese texto y el cliente falso registra el mensaje recibido

#### Scenario: Error programado
- **GIVEN** un cliente falso programado para fallar como un error de autenticación del proveedor
- **WHEN** el código bajo prueba envía un mensaje
- **THEN** recibe ese error, igual que con el proveedor real

### Requirement: Registro de llamadas sin secretos ni contenidos
Cada llamada al modelo SHALL registrar el modelo solicitado, el modelo que respondió, la duración y los tokens de entrada y salida. Los registros MUST NOT contener la API key, ningún fragmento suyo ni los textos completos enviados o recibidos.

#### Scenario: Llamada correcta
- **GIVEN** una llamada al modelo que termina bien
- **WHEN** se revisan los registros
- **THEN** aparecen el modelo solicitado, el que respondió, la duración y los tokens, y no aparece el texto del mensaje ni de la respuesta

#### Scenario: Modelo remapeado por el proveedor
- **GIVEN** un modelo solicitado que el proveedor atiende con otro distinto
- **WHEN** se revisan los registros
- **THEN** el registro muestra los dos nombres y avisa de que no coinciden

### Requirement: Errores del proveedor sin filtrar la clave
Un error del proveedor SHALL presentarse y registrarse con su tipo y su código HTTP. El texto del error devuelto por el proveedor MUST NOT mostrarse ni registrarse, porque puede contener fragmentos de la API key.

#### Scenario: Clave rechazada
- **GIVEN** una API key que el proveedor rechaza con HTTP 401, y cuyo mensaje de error repite parte de la clave
- **WHEN** se envía un mensaje
- **THEN** el error mostrado indica que la autenticación falló y el código 401, y ni la salida ni los registros contienen ningún fragmento de la clave
