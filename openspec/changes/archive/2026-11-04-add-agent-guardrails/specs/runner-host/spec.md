## MODIFIED Requirements

### Requirement: Códigos de salida
El Runner SHALL terminar con un código de salida distinto para cada resultado:

- `0`: éxito;
- `2`: configuración inválida;
- `3`: fallo al llamar al modelo (autenticación, red o respuesta de error del proveedor);
- `4`: ejecución del agente parada por un guardrail;
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

### Requirement: Salida de una ejecución del agente
Tras ejecutar el agente, el Runner SHALL escribir por la salida estándar la respuesta final y un resumen con el motivo de parada, el número de llamadas al modelo, las tools pedidas con su resultado y los tokens de entrada y de salida. Los registros SHALL seguir yendo a la salida de errores, y cada línea registrada durante una ejecución SHALL mostrar su identificador de ejecución.

#### Scenario: Escenario feliz con cliente falso
- **GIVEN** el Runner con un cliente de chat falso que pide `list_products`, después `place_order` y luego responde "Pedido ORD-0001 creado"
- **WHEN** se ejecuta con `--scenario happy-order`
- **THEN** termina con código 0, y la salida estándar muestra "Pedido ORD-0001 creado", el motivo `Completed`, 3 llamadas al modelo, las dos tools con éxito y los tokens

#### Scenario: Solo lectura con cliente falso
- **GIVEN** el Runner con un cliente de chat falso que pide `list_products`, después `place_order` y luego responde
- **WHEN** se ejecuta con `--scenario read-only`
- **THEN** termina con código 0, y la salida estándar muestra `place_order` con `BLOCKED_BY_POLICY`

### Requirement: Argumentos de ejecución inválidos
Un escenario desconocido, `--scenario` y `--prompt` a la vez, o un texto vacío, de solo espacios o de más de 2.000 caracteres MUST detectarse al arrancar, sin llamar al modelo, y terminar con el código 2 y un mensaje que nombre la opción.

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

#### Scenario: Texto demasiado largo
- **GIVEN** cualquier configuración válida de DeepSeek
- **WHEN** se ejecuta con un `--prompt` de 2.001 caracteres
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra la opción de texto
