# order-operations Specification

## Purpose
Define las operaciones que la capa de aplicación expone sobre el dominio de ventas, separadas en commands (cambian estado) y queries (solo leen): su contrato, la idempotencia de los commands, las vistas compactas que devuelven y el estado in-memory determinista sobre el que trabajan. Las tools de la fase 4 se apoyan en este contrato.

## Requirements

### Requirement: Command PlaceOrder
`PlaceOrder` SHALL recibir una clave de idempotencia, el identificador del cliente y una lista de líneas (producto y cantidad). Si tiene éxito, SHALL crear un pedido en `Placed`, reservar su stock y devolver la vista del pedido. Además de los códigos de `order-domain`, SHALL devolver `CUSTOMER_NOT_FOUND` o `PRODUCT_NOT_FOUND` (tipo `NotFound`) si el cliente o un producto no existen.

#### Scenario: Pedido creado
- **GIVEN** el cliente activo `CUST-001` y un producto con stock suficiente
- **WHEN** se ejecuta `PlaceOrder` con una clave nueva y una línea de 2 unidades
- **THEN** devuelve la vista de un pedido en `Placed` con esa línea y su importe

#### Scenario: Cliente inexistente
- **GIVEN** que no existe el cliente `CUST-999`
- **WHEN** se ejecuta `PlaceOrder` para `CUST-999`
- **THEN** devuelve `CUSTOMER_NOT_FOUND` y no se crea ningún pedido

#### Scenario: Producto inexistente
- **GIVEN** que no existe el producto `SKU-NOPE`
- **WHEN** se ejecuta `PlaceOrder` con una línea de `SKU-NOPE`
- **THEN** devuelve `PRODUCT_NOT_FOUND`, el mensaje nombra `SKU-NOPE` y no se reserva stock

### Requirement: Orden de evaluación de PlaceOrder
Cuando una petición de `PlaceOrder` incumple varias reglas, SHALL devolver solo el primer error en este orden: clave de idempotencia, líneas (`EMPTY_ORDER`, `INVALID_QUANTITY`, `DUPLICATE_ORDER_LINE`), existencia del cliente, cliente bloqueado, existencia de cada producto en el orden de las líneas, importe máximo y stock de cada línea en el orden de las líneas.

#### Scenario: Cliente bloqueado y stock insuficiente a la vez
- **GIVEN** un cliente bloqueado y una línea que pide más del disponible
- **WHEN** se ejecuta `PlaceOrder`
- **THEN** devuelve `CUSTOMER_BLOCKED`, no `INSUFFICIENT_STOCK`

#### Scenario: Cantidad inválida y cliente inexistente a la vez
- **GIVEN** un cliente inexistente y una línea con cantidad 0
- **WHEN** se ejecuta `PlaceOrder`
- **THEN** devuelve `INVALID_QUANTITY`

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

### Requirement: Vistas compactas
Las operaciones SHALL devolver vistas con solo los campos que necesita quien las consume. Pedido: identificador, cliente, estado, líneas (producto, cantidad, precio unitario e importe), importe total, moneda y fecha de creación. Cliente: identificador, nombre y estado. Stock: producto, nombre, precio unitario y disponible.

#### Scenario: La vista de stock no expone reservas
- **GIVEN** un producto con reservas
- **WHEN** se ejecuta `GetStock`
- **THEN** la vista muestra el disponible, pero no las existencias ni las reservas por separado

### Requirement: Idempotencia de los commands
Repetir un command con la misma clave de idempotencia y los mismos argumentos que una ejecución anterior con éxito SHALL devolver el mismo resultado que la primera vez, sin volver a ejecutarlo ni producir efectos nuevos.

#### Scenario: Reintento de PlaceOrder
- **GIVEN** un `PlaceOrder` con la clave `k1` que creó `ORD-0001`
- **WHEN** se repite `PlaceOrder` con `k1` y los mismos argumentos
- **THEN** devuelve la vista de `ORD-0001`, no se crea otro pedido y el stock no cambia

#### Scenario: Reintento de CancelOrder
- **GIVEN** un `CancelOrder` de `ORD-0001` con la clave `k2` que lo dejó en `Cancelled`
- **WHEN** se repite `CancelOrder` de `ORD-0001` con `k2`
- **THEN** devuelve con éxito la vista del pedido en `Cancelled`, no `INVALID_STATE_TRANSITION`

#### Scenario: Reintento después de un cambio de estado
- **GIVEN** un `PlaceOrder` con la clave `k1` que creó `ORD-0001`, cancelado después con la clave `k2`
- **WHEN** se repite `PlaceOrder` con `k1` y los mismos argumentos
- **THEN** devuelve la vista guardada de la primera vez, con `ORD-0001` en `Placed`, y el pedido sigue en `Cancelled`

#### Scenario: Otra clave sobre un pedido ya cancelado
- **GIVEN** `ORD-0001` cancelado con la clave `k2`
- **WHEN** se ejecuta `CancelOrder` de `ORD-0001` con la clave `k3`
- **THEN** devuelve `INVALID_STATE_TRANSITION`

### Requirement: Conflicto de clave de idempotencia
Reutilizar una clave ya registrada con argumentos distintos, o en un command distinto, MUST devolver `IDEMPOTENCY_KEY_CONFLICT` (tipo `Conflict`) sin ejecutar nada. Las mismas líneas en otro orden cuentan como argumentos distintos.

#### Scenario: Misma clave, otros argumentos
- **GIVEN** un `PlaceOrder` con la clave `k1` que tuvo éxito con 2 unidades
- **WHEN** se ejecuta `PlaceOrder` con `k1` y 3 unidades
- **THEN** devuelve `IDEMPOTENCY_KEY_CONFLICT` y no se crea ningún pedido

#### Scenario: Mismas líneas en otro orden
- **GIVEN** un `PlaceOrder` con la clave `k1` que tuvo éxito con las líneas `SKU-MUG-RED` ×2 y `SKU-TSHIRT-M` ×1
- **WHEN** se ejecuta `PlaceOrder` con `k1`, el mismo cliente y las líneas `SKU-TSHIRT-M` ×1 y `SKU-MUG-RED` ×2
- **THEN** devuelve `IDEMPOTENCY_KEY_CONFLICT` y no se crea ningún pedido

#### Scenario: Misma clave en otro command
- **GIVEN** un `PlaceOrder` con la clave `k1` que tuvo éxito
- **WHEN** se ejecuta `CancelOrder` con `k1`
- **THEN** devuelve `IDEMPOTENCY_KEY_CONFLICT` y el pedido no cambia

### Requirement: Solo se recuerdan los éxitos
Un command que termina con error de dominio MUST NOT registrar su clave de idempotencia. Un reintento con la misma clave SHALL evaluarse de nuevo contra el estado actual.

#### Scenario: Reintento tras stock insuficiente
- **GIVEN** un `PlaceOrder` con la clave `k1` que devolvió `INSUFFICIENT_STOCK`, y después la cancelación de otro pedido libera stock suficiente
- **WHEN** se repite `PlaceOrder` con `k1` y los mismos argumentos
- **THEN** el pedido se crea

### Requirement: Formato de la clave de idempotencia
La clave de idempotencia SHALL ser obligatoria en los commands, con entre 1 y 64 caracteres, sin caracteres de control y no formada solo por espacios. Una clave que no cumpla MUST devolver `INVALID_IDEMPOTENCY_KEY` (tipo `Validation`) sin ejecutar nada.

#### Scenario: Clave vacía
- **GIVEN** una clave vacía o de solo espacios
- **WHEN** se ejecuta cualquier command
- **THEN** devuelve `INVALID_IDEMPOTENCY_KEY`

#### Scenario: Clave demasiado larga
- **GIVEN** una clave de 65 caracteres
- **WHEN** se ejecuta `ShipOrder`
- **THEN** devuelve `INVALID_IDEMPOTENCY_KEY` y el pedido no cambia

### Requirement: Identificadores de pedido secuenciales
Los pedidos SHALL numerarse en orden de creación con el formato `ORD-` seguido de cuatro dígitos, empezando por `ORD-0001` en cada estado nuevo. Un intento fallido o un reintento idempotente MUST NOT consumir número.

#### Scenario: Numeración tras un fallo
- **GIVEN** un estado nuevo
- **WHEN** se crea un pedido, falla un segundo intento por stock insuficiente y se crea un tercero
- **THEN** los pedidos creados son `ORD-0001` y `ORD-0002`

### Requirement: Reloj inyectado
La fecha de creación de un pedido SHALL tomarse de un reloj inyectable, de modo que un test pueda fijarla.

#### Scenario: Fecha fijada por el test
- **GIVEN** un reloj falso fijado en 2026-11-01T10:00:00Z
- **WHEN** se crea un pedido
- **THEN** su fecha de creación es 2026-11-01T10:00:00Z

### Requirement: Catálogo semilla
El estado inicial por defecto SHALL ser un catálogo fijo, igual en cada ejecución. Debe tener al menos tres clientes, uno de ellos bloqueado, y al menos tres productos, uno sin stock disponible. Los tests SHALL poder partir de un estado construido a medida en lugar del catálogo.

#### Scenario: Catálogo reproducible
- **GIVEN** dos estados nuevos con el catálogo semilla
- **WHEN** se consultan sus clientes y productos
- **THEN** los dos tienen los mismos identificadores, nombres, precios y disponibles, con un cliente bloqueado y un producto sin stock

### Requirement: Atomicidad frente a llamadas concurrentes
Cada command SHALL aplicarse de forma atómica sobre el estado: dos commands concurrentes MUST NOT dejar reservas, existencias o numeración inconsistentes.

#### Scenario: Dos pedidos compiten por el último stock
- **GIVEN** un producto con 5 disponibles
- **WHEN** se ejecutan a la vez dos `PlaceOrder` de 5 unidades con claves distintas
- **THEN** exactamente uno se crea, el otro devuelve `INSUFFICIENT_STOCK`, y el disponible queda en 0

### Requirement: Cancelación de la llamada
Las operaciones SHALL aceptar una señal de cancelación. Una operación cancelada antes de aplicar sus efectos MUST propagar la cancelación al llamante, no convertirla en un error de dominio, y MUST NOT dejar efectos.

#### Scenario: Cancelación antes de ejecutar
- **GIVEN** una señal de cancelación ya activada
- **WHEN** se ejecuta `PlaceOrder`
- **THEN** la llamada termina por cancelación y no se crea ningún pedido

### Requirement: Query ListProducts
`ListProducts` SHALL devolver todos los productos del catálogo con la vista de stock de cada uno (producto, nombre, precio unitario y disponible), ordenados por SKU con comparación ordinal. Como el resto de queries, MUST NOT cambiar ningún estado.

#### Scenario: Catálogo semilla
- **GIVEN** el catálogo semilla
- **WHEN** se ejecuta `ListProducts`
- **THEN** devuelve los cuatro productos ordenados por SKU, con sus disponibles: `SKU-CHAIR-ERGO` 10, `SKU-LAMP-DESK` 0, `SKU-MUG-RED` 20 y `SKU-TSHIRT-M` 5

#### Scenario: Refleja las reservas
- **GIVEN** un pedido en curso de 3 unidades de `SKU-MUG-RED`
- **WHEN** se ejecuta `ListProducts`
- **THEN** `SKU-MUG-RED` aparece con 17 disponibles y el estado no cambia

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
