# Spec Delta

## ADDED Requirements

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
