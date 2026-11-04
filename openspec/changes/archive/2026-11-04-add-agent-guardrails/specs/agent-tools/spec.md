## MODIFIED Requirements

### Requirement: Catálogo de tools expuestas
El catálogo de tools del agente SHALL estar formado exactamente por estas tools, con estos nombres estables: `list_products`, `get_stock`, `get_customer`, `get_order`, `place_order` y `cancel_order`. Al modelo SHALL ofrecérsele las del catálogo que permita la allow-list de `agent-guardrails`; por defecto, todas. El envío de pedidos MUST NOT exponerse como tool.

#### Scenario: Tools disponibles para el modelo
- **GIVEN** el agente con la configuración por defecto
- **WHEN** se consulta la lista de tools que se envía al modelo
- **THEN** contiene exactamente esas seis, con esos nombres, y ninguna permite enviar un pedido

### Requirement: Tool get_order
`get_order` SHALL recibir el identificador de un pedido y devolver su vista si es del cliente de sesión. Si el pedido no existe o es de otro cliente, SHALL devolver `ORDER_NOT_FOUND`, sin distinguir un caso del otro.

#### Scenario: Pedido inexistente
- **GIVEN** que no existe `ORD-9999`
- **WHEN** el modelo invoca `get_order` con `ORD-9999`
- **THEN** recibe `ORDER_NOT_FOUND`

#### Scenario: Pedido de otro cliente
- **GIVEN** el cliente de sesión `CUST-002` y `ORD-0001`, del cliente `CUST-001`
- **WHEN** el modelo invoca `get_order` con `ORD-0001`
- **THEN** recibe `ORDER_NOT_FOUND` con el mismo mensaje que para `ORD-9999`, sin datos del pedido

### Requirement: Tool cancel_order
`cancel_order` SHALL recibir el identificador del pedido y una clave de idempotencia, y cancelar el pedido en nombre del cliente de sesión con las reglas y la idempotencia de `order-operations`. Además de `SERVICE_UNAVAILABLE` e `INTERNAL_ERROR`, sus códigos de error SHALL ser `ORDER_NOT_FOUND` (también para un pedido de otro cliente), `INVALID_STATE_TRANSITION`, `INVALID_IDEMPOTENCY_KEY` e `IDEMPOTENCY_KEY_CONFLICT`.

#### Scenario: Cancelación de un pedido en curso
- **GIVEN** un pedido en `Placed` del cliente de sesión
- **WHEN** el modelo invoca `cancel_order` con una clave nueva
- **THEN** recibe el pedido en estado `Cancelled`

#### Scenario: Pedido ya cancelado con otra clave
- **GIVEN** un pedido del cliente de sesión cancelado con la clave `k1`
- **WHEN** el modelo invoca `cancel_order` sobre él con la clave `k2`
- **THEN** recibe `INVALID_STATE_TRANSITION` no reintentable

#### Scenario: Pedido de otro cliente
- **GIVEN** el cliente de sesión `CUST-002` y `ORD-0001` en `Placed`, del cliente `CUST-001`
- **WHEN** el modelo invoca `cancel_order` con `ORD-0001` y una clave nueva
- **THEN** recibe `ORDER_NOT_FOUND` y `ORD-0001` sigue en `Placed`

### Requirement: Registro de cada invocación
Cada invocación de una tool SHALL dejar un evento de auditoría (ver `agent-guardrails`) con el nombre de la tool, la decisión (`Allowed` si se ejecutó, `Blocked` si la allow-list lo impidió), la duración y el resultado (éxito o código de error). El evento MUST NOT incluir los argumentos de la invocación ni los datos del resultado.

#### Scenario: Invocación fallida
- **GIVEN** una invocación de `place_order` que devuelve `INSUFFICIENT_STOCK`
- **WHEN** se revisa la auditoría
- **THEN** contiene un evento con `place_order`, la decisión `Allowed`, su duración y `INSUFFICIENT_STOCK`, sin el producto, la cantidad ni la clave

#### Scenario: Invocación bloqueada
- **GIVEN** `place_order` fuera de la allow-list y un modelo que la pide
- **WHEN** se revisa la auditoría
- **THEN** contiene un evento con `place_order`, la decisión `Blocked` y `BLOCKED_BY_POLICY`
