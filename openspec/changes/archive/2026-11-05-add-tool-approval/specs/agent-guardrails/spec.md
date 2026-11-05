## MODIFIED Requirements

### Requirement: Límite de errores de tool seguidos
Cuando `MaxConsecutiveToolErrors` invocaciones de tools seguidas terminan con un error, de cualquier código e incluidos `BLOCKED_BY_POLICY` y `CONFIRMATION_DENIED`, la ejecución SHALL terminar tras la última de ellas, sin volver a llamar al modelo, con el motivo de parada `ToolErrorLimit`. Una invocación con éxito SHALL reiniciar la cuenta.

#### Scenario: Tres errores seguidos
- **GIVEN** `MaxConsecutiveToolErrors` igual a 3 y un modelo que pide `get_order` de `ORD-9999` en cada respuesta
- **WHEN** el agente atiende la petición
- **THEN** `get_order` devuelve `ORDER_NOT_FOUND` 3 veces, el modelo recibe 3 llamadas y el motivo de parada es `ToolErrorLimit`

#### Scenario: Un éxito reinicia la cuenta
- **GIVEN** `MaxConsecutiveToolErrors` igual a 3 y un modelo que pide, en respuestas sucesivas, `get_order` de `ORD-9999` dos veces, `list_products`, `get_order` de `ORD-9999` dos veces y después responde con texto
- **WHEN** el agente atiende la petición
- **THEN** el motivo de parada es `Completed`

#### Scenario: Confirmaciones denegadas seguidas
- **GIVEN** `MaxConsecutiveToolErrors` igual a 3, un aprobador que siempre deniega y un modelo que pide `cancel_order` de un pedido del cliente de sesión en cada respuesta
- **WHEN** el agente atiende la petición
- **THEN** se pregunta 3 veces al aprobador, el pedido no cambia y el motivo de parada es `ToolErrorLimit`
