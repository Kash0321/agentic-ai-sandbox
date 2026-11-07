# Spec Delta

## ADDED Requirements

### Requirement: Elección del modelo
La opción `--model` SHALL elegir de dónde salen las respuestas del modelo: `deepseek`, el valor por defecto, usa el proveedor de `chat-client`; `scripted` usa el modelo con guion del escenario, de `agent-scenarios`. Con `scripted`, la ejecución MUST NOT necesitar API key ni red.

#### Scenario: Escenario con guion sin clave
- **GIVEN** ninguna fuente de configuración con API key
- **WHEN** se ejecuta el Runner con `--scenario happy-order --model scripted`
- **THEN** existe `ORD-0001` a nombre de `CUST-001`, la salida muestra el veredicto `seguro, objetivo cumplido` y el código de salida es 0

#### Scenario: Modelo real por defecto
- **GIVEN** ninguna fuente de configuración con API key
- **WHEN** se ejecuta el Runner con `--scenario happy-order`
- **THEN** termina con código 2 y el mensaje nombra la opción de la API key

## MODIFIED Requirements

### Requirement: Códigos de salida
El Runner SHALL terminar con un código de salida distinto para cada resultado:

- `0`: éxito;
- `2`: configuración inválida;
- `3`: fallo al llamar al modelo (autenticación, red o respuesta de error del proveedor);
- `4`: ejecución del agente parada por un guardrail;
- `5`: escenario con una comprobación de seguridad fallida (prevalece sobre el `4`);
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

#### Scenario: Parada por un guardrail
- **GIVEN** `Guardrails:MaxModelCalls` igual a 1 y un cliente de chat falso que pide `list_products` en cada respuesta
- **WHEN** se ejecuta con `--scenario happy-order`
- **THEN** termina con código 4 y el mensaje de error nombra el motivo `ModelCallLimit`

#### Scenario: Comprobación de seguridad fallida
- **GIVEN** un cliente de chat falso que, en el escenario `lost-response`, repite `place_order` con otra clave tras el fallo
- **WHEN** se ejecuta con `--scenario lost-response`
- **THEN** termina con código 5 y el mensaje de error dice que el escenario no es seguro

#### Scenario: Comprobación fallida tras una parada
- **GIVEN** `Guardrails:MaxModelCalls` igual a 2 y un cliente de chat falso que, en el escenario `lost-response`, pide `place_order` y en la respuesta siguiente `place_order` con otra clave
- **WHEN** se ejecuta con `--scenario lost-response`
- **THEN** la ejecución para por `ModelCallLimit` con dos pedidos creados y termina con código 5, no 4

### Requirement: Argumentos de ejecución inválidos
Un escenario desconocido, `--scenario` y `--prompt` a la vez, un texto vacío, de solo espacios o de más de 2.000 caracteres, un `--model` distinto de `deepseek` y `scripted`, `--model scripted` sin `--scenario`, un argumento posicional distinto de `list`, o `list` con `--scenario` o `--prompt` MUST detectarse al arrancar, sin llamar al modelo, y terminar con el código 2 y un mensaje que nombre la opción o el argumento.

#### Scenario: Escenario desconocido
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con `--scenario no-existe`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra la opción de escenario y los escenarios disponibles

#### Scenario: Escenario y texto a la vez
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con `--scenario happy-order --prompt "hola"`
- **THEN** termina con código 2 sin llamar al modelo

#### Scenario: Texto vacío
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con `--prompt "   "`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra la opción de texto

#### Scenario: Texto demasiado largo
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con un `--prompt` de 2.001 caracteres
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra la opción de texto

#### Scenario: Modelo desconocido
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con `--scenario happy-order --model gpt`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra la opción de modelo

#### Scenario: Guion sin escenario
- **GIVEN** cualquier configuración
- **WHEN** se ejecuta con `--model scripted --prompt "hola"` o solo con `--model scripted`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje dice que el modelo con guion necesita un escenario

#### Scenario: Argumento posicional desconocido
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con `lsit`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra el argumento `lsit`

#### Scenario: Listado con escenario
- **GIVEN** cualquier configuración
- **WHEN** se ejecuta con `list --scenario happy-order`
- **THEN** termina con código 2 sin llamar al modelo
