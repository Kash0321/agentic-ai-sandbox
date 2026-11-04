## MODIFIED Requirements

### Requirement: Commands CancelOrder y ShipOrder
`CancelOrder` y `ShipOrder` SHALL recibir una clave de idempotencia y el identificador del pedido, aplicar la transición correspondiente de `order-domain` y devolver la vista del pedido. `CancelOrder` SHALL recibir además el cliente que pide la cancelación. Si el pedido no existe, o en `CancelOrder` es de otro cliente, MUST devolver `ORDER_NOT_FOUND` (tipo `NotFound`) sin cambiar nada.

#### Scenario: Cancelar un pedido existente
- **GIVEN** un pedido en `Placed` del cliente `CUST-001`
- **WHEN** se ejecuta `CancelOrder` pedido por `CUST-001` con una clave nueva
- **THEN** devuelve la vista del pedido en `Cancelled`

#### Scenario: Enviar un pedido existente
- **GIVEN** un pedido en `Placed`
- **WHEN** se ejecuta `ShipOrder` con una clave nueva
- **THEN** devuelve la vista del pedido en `Shipped`

#### Scenario: Pedido inexistente
- **GIVEN** que no existe el pedido `ORD-9999`
- **WHEN** se ejecuta `CancelOrder` o `ShipOrder` sobre `ORD-9999`
- **THEN** devuelve `ORDER_NOT_FOUND`

#### Scenario: Cancelar el pedido de otro cliente
- **GIVEN** `ORD-0001` en `Placed`, del cliente `CUST-001`
- **WHEN** se ejecuta `CancelOrder` de `ORD-0001` pedido por `CUST-002`
- **THEN** devuelve `ORDER_NOT_FOUND` con el mismo mensaje que un pedido inexistente, el pedido sigue en `Placed` y su stock sigue reservado

### Requirement: Queries de solo lectura
`GetOrder` (por identificador de pedido y cliente que consulta), `GetCustomer` (por identificador de cliente) y `GetStock` (por producto) SHALL devolver la vista correspondiente, o `ORDER_NOT_FOUND`, `CUSTOMER_NOT_FOUND` o `PRODUCT_NOT_FOUND` si no existe. `GetOrder` MUST devolver `ORDER_NOT_FOUND` también cuando el pedido es de otro cliente. Una query MUST NOT cambiar ningún estado ni consumir números de pedido.

#### Scenario: Consultar el stock
- **GIVEN** un producto con 20 existencias y 3 reservadas
- **WHEN** se ejecuta `GetStock` para ese producto
- **THEN** devuelve su nombre, su precio unitario y 17 disponibles

#### Scenario: Consultar un pedido inexistente
- **GIVEN** que no existe el pedido `ORD-9999`
- **WHEN** se ejecuta `GetOrder` sobre `ORD-9999`
- **THEN** devuelve `ORDER_NOT_FOUND`

#### Scenario: Consultar el pedido de otro cliente
- **GIVEN** `ORD-0001`, del cliente `CUST-001`
- **WHEN** se ejecuta `GetOrder` de `ORD-0001` consultado por `CUST-002`
- **THEN** devuelve `ORDER_NOT_FOUND` con el mismo mensaje que un pedido inexistente

#### Scenario: Las queries no cambian el estado
- **GIVEN** un estado con pedidos y reservas
- **WHEN** se ejecutan varias queries
- **THEN** los pedidos, el stock y el siguiente número de pedido siguen iguales

## ADDED Requirements

### Requirement: Ámbito de las claves de idempotencia
Cada clave de idempotencia SHALL registrarse en un ámbito: el cliente en `PlaceOrder` y `CancelOrder`, y el almacén en `ShipOrder`. La repetición y el conflicto de claves SHALL evaluarse solo dentro del mismo ámbito: la misma clave en dos ámbitos son dos claves independientes.

#### Scenario: Otro cliente repite la misma petición
- **GIVEN** `ORD-0001` del cliente `CUST-001`, cancelado por `CUST-001` con la clave `k1`
- **WHEN** se ejecuta `CancelOrder` de `ORD-0001` pedido por `CUST-002` con la clave `k1`
- **THEN** devuelve `ORDER_NOT_FOUND`, no la vista guardada del pedido

#### Scenario: Dos clientes con la misma clave
- **GIVEN** un `PlaceOrder` de `CUST-001` con la clave `k1` que creó `ORD-0001`
- **WHEN** se ejecuta `PlaceOrder` de `CUST-002` con la clave `k1` y otras líneas
- **THEN** se crea `ORD-0002` para `CUST-002`, sin `IDEMPOTENCY_KEY_CONFLICT`

### Requirement: Auditoría de los accesos denegados
Cuando `GetOrder` o `CancelOrder` devuelven `ORDER_NOT_FOUND` porque el pedido es de otro cliente, SHALL registrarse un evento de auditoría (ver `agent-guardrails`) con la operación, el identificador del pedido, el cliente que lo pidió y el motivo "pedido de otro cliente". Un pedido que no existe MUST NOT registrarse como acceso denegado.

#### Scenario: Acceso a un pedido ajeno
- **GIVEN** `ORD-0001`, del cliente `CUST-001`
- **WHEN** se ejecuta `GetOrder` de `ORD-0001` consultado por `CUST-002`
- **THEN** la auditoría contiene un evento de acceso denegado con `GetOrder`, `ORD-0001`, `CUST-002` y el motivo

#### Scenario: Pedido inexistente
- **GIVEN** que no existe el pedido `ORD-9999`
- **WHEN** se ejecuta `GetOrder` de `ORD-9999`
- **THEN** la auditoría no contiene ningún evento de acceso denegado
