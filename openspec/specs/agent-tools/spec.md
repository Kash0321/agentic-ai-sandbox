# agent-tools Specification

## Purpose
Define el contrato de las herramientas (tools) que el modelo puede invocar sobre el dominio de ventas: qué tools existen, sus parámetros, el resultado que devuelven, los códigos de error estables y cómo se comportan ante lo inesperado. Las tools son finas: traducen argumentos a operaciones de la aplicación y resultados a un formato que el modelo pueda usar, sin reglas de negocio propias.

## Requirements

### Requirement: Catálogo de tools expuestas
El agente SHALL exponer al modelo exactamente estas tools, con estos nombres estables: `list_products`, `get_stock`, `get_customer`, `get_order`, `place_order` y `cancel_order`. El envío de pedidos MUST NOT exponerse como tool.

#### Scenario: Tools disponibles para el modelo
- **GIVEN** el agente configurado
- **WHEN** se consulta la lista de tools que se envía al modelo
- **THEN** contiene exactamente esas seis, con esos nombres, y ninguna permite enviar un pedido

### Requirement: Descripciones y esquema de parámetros
Cada tool y cada uno de sus parámetros SHALL llevar una descripción no vacía, escrita para el modelo. El esquema de parámetros de cada tool SHALL marcar como obligatorios todos sus parámetros y MUST NOT incluir parámetros internos, como la señal de cancelación.

#### Scenario: Esquema de place_order
- **GIVEN** la tool `place_order`
- **WHEN** se inspecciona su esquema de parámetros
- **THEN** declara como obligatorios las líneas (lista de objetos con producto y cantidad) y la clave de idempotencia, cada uno con su descripción, y nada más

#### Scenario: Todas las tools descritas
- **GIVEN** las seis tools
- **WHEN** se inspeccionan sus descripciones y las de sus parámetros
- **THEN** ninguna está vacía

### Requirement: Resultado tipado de las tools
Toda tool SHALL devolver un resultado con un indicador de éxito y, según el caso, los datos o un error. El error SHALL llevar un código estable, un mensaje breve y la indicación de si reintentar la misma petición puede funcionar. En la representación que recibe el modelo, un éxito MUST NOT incluir error, un fallo MUST NOT incluir datos, y los estados SHALL aparecer como texto (por ejemplo, `Placed`), no como números.

#### Scenario: Éxito
- **GIVEN** un pedido creado con éxito a través de `place_order`
- **WHEN** el modelo recibe el resultado
- **THEN** ve el indicador de éxito, el pedido con su estado como texto, y ningún error

#### Scenario: Fallo
- **GIVEN** una llamada a `get_stock` con un producto inexistente
- **WHEN** el modelo recibe el resultado
- **THEN** ve el fallo con el código `PRODUCT_NOT_FOUND`, un mensaje que nombra el producto, la indicación de no reintentable y ningún dato

### Requirement: Errores reintentables
La indicación de reintento de un error SHALL ser verdadera solo para los errores transitorios del dominio (`SERVICE_UNAVAILABLE`) y falsa para todos los demás.

#### Scenario: Fallo transitorio inducido
- **GIVEN** una regla del inyector que hace fallar `GetStock`
- **WHEN** el modelo invoca `get_stock`
- **THEN** recibe `SERVICE_UNAVAILABLE` marcado como reintentable

#### Scenario: Regla de negocio incumplida
- **GIVEN** un producto sin stock suficiente
- **WHEN** el modelo invoca `place_order` con ese producto
- **THEN** recibe `INSUFFICIENT_STOCK` marcado como no reintentable

### Requirement: Sin excepciones hacia el modelo
Una tool MUST NOT propagar al modelo ninguna excepción. Un fallo inesperado SHALL devolverse como el error `INTERNAL_ERROR`, no reintentable, con un mensaje genérico sin detalles internos, y su detalle SHALL quedar en el registro de errores. La cancelación de la ejecución MUST propagarse como cancelación, no convertirse en error.

#### Scenario: Fallo inesperado de la aplicación
- **GIVEN** una operación de aplicación que lanza una excepción imprevista
- **WHEN** el modelo invoca la tool correspondiente
- **THEN** recibe `INTERNAL_ERROR` no reintentable, sin la traza ni el mensaje de la excepción, y el registro contiene la excepción

#### Scenario: Ejecución cancelada
- **GIVEN** una ejecución cancelada mientras se invoca una tool
- **WHEN** la tool se ejecuta
- **THEN** la cancelación llega a quien ejecutó el agente y no se devuelve ningún resultado al modelo

### Requirement: Argumentos que no cumplen el esquema
Si el modelo invoca una tool con argumentos que no se pueden asignar a sus parámetros (falta uno obligatorio o tiene otro tipo), el modelo SHALL recibir el error `INVALID_ARGUMENTS`, no reintentable, con un mensaje que nombra la tool y pide revisar los parámetros. El mensaje MUST NOT incluir el texto interno de la excepción. La tool MUST NOT ejecutarse.

#### Scenario: Falta un parámetro obligatorio
- **GIVEN** una invocación de `get_stock` sin el SKU
- **WHEN** el agente la ejecuta
- **THEN** el modelo recibe `INVALID_ARGUMENTS` no reintentable, con un mensaje que nombra `get_stock`, y no se consulta el stock

### Requirement: Cliente de sesión
Las tools SHALL actuar en nombre de un cliente de sesión fijado fuera del modelo. Ninguna tool MUST recibir el identificador del cliente como parámetro.

#### Scenario: El modelo no elige el cliente
- **GIVEN** el cliente de sesión `CUST-002`
- **WHEN** el modelo crea un pedido con `place_order`
- **THEN** el pedido queda a nombre de `CUST-002`, y ninguna tool ofrece un parámetro para indicar otro cliente

### Requirement: Tool list_products
`list_products` SHALL no recibir parámetros y devolver todos los productos del catálogo, cada uno con su SKU, nombre, precio unitario en euros y unidades disponibles.

#### Scenario: Catálogo semilla
- **GIVEN** el catálogo semilla
- **WHEN** el modelo invoca `list_products`
- **THEN** recibe los cuatro productos con su disponible, incluido uno con 0 unidades

### Requirement: Tool get_stock
`get_stock` SHALL recibir el SKU de un producto y devolver su nombre, precio unitario y disponible. Si el producto no existe, SHALL devolver `PRODUCT_NOT_FOUND`.

#### Scenario: Producto existente
- **GIVEN** el producto `SKU-MUG-RED` con 20 disponibles
- **WHEN** el modelo invoca `get_stock` con ese SKU
- **THEN** recibe 20 disponibles y su precio de 12,50

### Requirement: Tool get_customer
`get_customer` SHALL no recibir parámetros y devolver el identificador, el nombre y el estado del cliente de sesión. Si el cliente de sesión no existe, SHALL devolver `CUSTOMER_NOT_FOUND`.

#### Scenario: Cliente bloqueado
- **GIVEN** el cliente de sesión `CUST-003`, que está bloqueado
- **WHEN** el modelo invoca `get_customer`
- **THEN** recibe su nombre y el estado `Blocked`

### Requirement: Tool get_order
`get_order` SHALL recibir el identificador de un pedido y devolver su vista. Si el pedido no existe, SHALL devolver `ORDER_NOT_FOUND`.

#### Scenario: Pedido inexistente
- **GIVEN** que no existe `ORD-9999`
- **WHEN** el modelo invoca `get_order` con `ORD-9999`
- **THEN** recibe `ORDER_NOT_FOUND`

### Requirement: Tool place_order
`place_order` SHALL recibir las líneas del pedido (producto y cantidad) y una clave de idempotencia, y crear el pedido para el cliente de sesión con las reglas y la idempotencia de `order-operations`.

#### Scenario: Pedido creado
- **GIVEN** el cliente de sesión `CUST-001` y stock suficiente
- **WHEN** el modelo invoca `place_order` con 2 unidades de `SKU-MUG-RED` y una clave nueva
- **THEN** recibe el pedido en estado `Placed` a nombre de `CUST-001`, con importe 25,00

#### Scenario: Reintento con la misma clave
- **GIVEN** un `place_order` con la clave `k1` que ya creó `ORD-0001`
- **WHEN** el modelo repite `place_order` con `k1` y las mismas líneas
- **THEN** recibe `ORD-0001` con éxito y no se crea ningún otro pedido

#### Scenario: Cliente de sesión bloqueado
- **GIVEN** el cliente de sesión `CUST-003`, bloqueado
- **WHEN** el modelo invoca `place_order`
- **THEN** recibe `CUSTOMER_BLOCKED` no reintentable

### Requirement: Códigos de error de place_order
Además de `SERVICE_UNAVAILABLE` e `INTERNAL_ERROR`, `place_order` SHALL devolver los códigos de la operación de creación, y solo esos:
- `EMPTY_ORDER`, `INVALID_QUANTITY` y `DUPLICATE_ORDER_LINE`;
- `CUSTOMER_NOT_FOUND` y `CUSTOMER_BLOCKED`;
- `PRODUCT_NOT_FOUND`, `ORDER_LIMIT_EXCEEDED` e `INSUFFICIENT_STOCK`;
- `INVALID_IDEMPOTENCY_KEY` e `IDEMPOTENCY_KEY_CONFLICT`.

#### Scenario: Cada error de la operación llega al modelo
- **GIVEN** una petición de `place_order` que incumple una regla de la operación de creación
- **WHEN** el modelo invoca la tool
- **THEN** recibe el código de esa regla, el mismo que devolvería la operación, con su mensaje

### Requirement: Descripción de la clave de idempotencia
La descripción del parámetro de clave de idempotencia de `place_order` y de `cancel_order` SHALL pedir al modelo una clave nueva para cada petición distinta y exactamente la misma al repetir una petición tras un error reintentable.

#### Scenario: Descripción de la clave
- **GIVEN** las tools `place_order` y `cancel_order`
- **WHEN** se inspecciona la descripción de su clave de idempotencia
- **THEN** pide una clave nueva por petición distinta y la misma al reintentar

### Requirement: Tool cancel_order
`cancel_order` SHALL recibir el identificador del pedido y una clave de idempotencia, y cancelar el pedido con las reglas y la idempotencia de `order-operations`. Además de `SERVICE_UNAVAILABLE` e `INTERNAL_ERROR`, sus códigos de error SHALL ser `ORDER_NOT_FOUND`, `INVALID_STATE_TRANSITION`, `INVALID_IDEMPOTENCY_KEY` e `IDEMPOTENCY_KEY_CONFLICT`.

#### Scenario: Cancelación de un pedido en curso
- **GIVEN** un pedido en `Placed`
- **WHEN** el modelo invoca `cancel_order` con una clave nueva
- **THEN** recibe el pedido en estado `Cancelled`

#### Scenario: Pedido ya cancelado con otra clave
- **GIVEN** un pedido cancelado con la clave `k1`
- **WHEN** el modelo invoca `cancel_order` sobre él con la clave `k2`
- **THEN** recibe `INVALID_STATE_TRANSITION` no reintentable

### Requirement: Registro de cada invocación
Cada invocación de una tool SHALL registrarse con el nombre de la tool, la duración y el resultado (éxito o código de error). El registro MUST NOT incluir los argumentos de la invocación ni los datos del resultado.

#### Scenario: Invocación fallida
- **GIVEN** una invocación de `place_order` que devuelve `INSUFFICIENT_STOCK`
- **WHEN** se revisa el registro
- **THEN** contiene una entrada con `place_order`, su duración y `INSUFFICIENT_STOCK`, sin el producto, la cantidad ni la clave
