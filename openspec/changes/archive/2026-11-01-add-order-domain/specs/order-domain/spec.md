# Spec Delta

## Purpose

Define las reglas de negocio del dominio de ventas simulado (pedidos, clientes y stock): la máquina de estados del pedido, las validaciones y el modelo de error con códigos estables. Son la última barrera ante una propuesta equivocada del modelo, porque el LLM propone y el dominio decide.

## ADDED Requirements

### Requirement: Errores de negocio como resultado
Toda regla de negocio o validación que no se cumpla SHALL devolverse como un resultado de error con un código estable en inglés y en mayúsculas, un tipo y un mensaje breve. El dominio MUST NOT lanzar excepciones por una regla o una validación. El tipo SHALL ser uno de `Validation`, `NotFound`, `BusinessRule`, `Conflict` o `Transient`. El mensaje MUST NOT incluir trazas, rutas ni nombres internos.

#### Scenario: Una regla incumplida no lanza excepción
- **GIVEN** un pedido en estado `Shipped`
- **WHEN** se intenta cancelar
- **THEN** se obtiene un resultado de error con código `INVALID_STATE_TRANSITION` y tipo `Conflict`, y no se lanza ninguna excepción

#### Scenario: El mismo incumplimiento da siempre el mismo código
- **GIVEN** dos ejecuciones independientes con el mismo estado inicial
- **WHEN** en las dos se pide más stock del disponible para un producto
- **THEN** las dos devuelven el código `INSUFFICIENT_STOCK` con el mismo tipo

### Requirement: Estado inicial del pedido
Un pedido recién creado SHALL quedar en estado `Placed`, con el cliente que lo pide y la fecha de creación.

#### Scenario: Pedido creado
- **GIVEN** un cliente activo y líneas válidas con stock suficiente
- **WHEN** se crea el pedido
- **THEN** su estado es `Placed` y guarda el identificador del cliente y la fecha de creación

### Requirement: Transiciones permitidas del pedido
Un pedido SHALL admitir solo dos transiciones: de `Placed` a `Shipped` (envío) y de `Placed` a `Cancelled` (cancelación). Cualquier otra transición MUST devolver `INVALID_STATE_TRANSITION` (tipo `Conflict`) y dejar el pedido y el stock sin cambios.

#### Scenario: Enviar un pedido en curso
- **GIVEN** un pedido en estado `Placed`
- **WHEN** se envía
- **THEN** su estado pasa a `Shipped`

#### Scenario: Cancelar un pedido en curso
- **GIVEN** un pedido en estado `Placed`
- **WHEN** se cancela
- **THEN** su estado pasa a `Cancelled`

#### Scenario: Cancelar un pedido enviado
- **GIVEN** un pedido en estado `Shipped`
- **WHEN** se cancela
- **THEN** se obtiene `INVALID_STATE_TRANSITION` y el pedido sigue en `Shipped`

#### Scenario: Cancelar un pedido ya cancelado
- **GIVEN** un pedido en estado `Cancelled`
- **WHEN** se cancela otra vez
- **THEN** se obtiene `INVALID_STATE_TRANSITION` y el stock no cambia

#### Scenario: Enviar un pedido cancelado o ya enviado
- **GIVEN** un pedido en estado `Cancelled` o `Shipped`
- **WHEN** se envía
- **THEN** se obtiene `INVALID_STATE_TRANSITION` y el pedido no cambia de estado

### Requirement: Validación de las líneas del pedido
Un pedido SHALL tener al menos una línea; si no, MUST devolver `EMPTY_ORDER`. Cada línea SHALL pedir una cantidad entera entre 1 y 100, ambos incluidos; si no, MUST devolver `INVALID_QUANTITY`. Un producto MUST NOT aparecer en dos líneas del mismo pedido; si aparece, MUST devolver `DUPLICATE_ORDER_LINE`. Los tres códigos son de tipo `Validation`.

#### Scenario: Pedido sin líneas
- **GIVEN** una petición de pedido sin líneas
- **WHEN** se intenta crear
- **THEN** se obtiene `EMPTY_ORDER`

#### Scenario: Cantidad fuera de rango
- **GIVEN** una línea con cantidad 0, otra petición con cantidad -1 y otra con cantidad 101
- **WHEN** se intenta crear cada pedido
- **THEN** cada intento devuelve `INVALID_QUANTITY`

#### Scenario: Cantidades en los límites
- **GIVEN** una línea con cantidad 1 y otra petición con cantidad 100, ambas con stock suficiente y dentro del importe máximo
- **WHEN** se crea cada pedido
- **THEN** los dos se crean

#### Scenario: Producto repetido
- **GIVEN** una petición con dos líneas del mismo producto
- **WHEN** se intenta crear el pedido
- **THEN** se obtiene `DUPLICATE_ORDER_LINE`

### Requirement: Cliente bloqueado
Un cliente bloqueado MUST NOT poder crear pedidos. El intento SHALL devolver `CUSTOMER_BLOCKED` (tipo `BusinessRule`) sin reservar stock.

#### Scenario: Pedido de un cliente bloqueado
- **GIVEN** un cliente en estado bloqueado y líneas válidas con stock suficiente
- **WHEN** intenta crear un pedido
- **THEN** se obtiene `CUSTOMER_BLOCKED` y el stock disponible no cambia

### Requirement: Importe máximo por pedido
El importe de un pedido SHALL ser la suma de cantidad por precio unitario de cada línea, en euros, con el precio del catálogo en el momento de crearlo. Un pedido cuyo importe supere 1.000,00 EUR MUST devolver `ORDER_LIMIT_EXCEEDED` (tipo `BusinessRule`). Un importe igual al límite SHALL aceptarse.

#### Scenario: Importe por encima del límite
- **GIVEN** líneas cuyo importe total es 1.000,01 EUR
- **WHEN** se intenta crear el pedido
- **THEN** se obtiene `ORDER_LIMIT_EXCEEDED` y no se reserva stock

#### Scenario: Importe igual al límite
- **GIVEN** líneas cuyo importe total es exactamente 1.000,00 EUR y stock suficiente
- **WHEN** se crea el pedido
- **THEN** el pedido se crea con importe 1.000,00 EUR

#### Scenario: El precio queda fijado al crear
- **GIVEN** un pedido creado con un producto a 12,50 EUR
- **WHEN** se consulta después el pedido
- **THEN** la línea conserva el precio unitario de 12,50 EUR y el importe calculado entonces

### Requirement: Reserva de stock al crear un pedido
Crear un pedido SHALL reservar la cantidad de cada línea. El stock disponible de un producto es sus existencias menos sus reservas. Si alguna línea pide más que el disponible, MUST devolver `INSUFFICIENT_STOCK` (tipo `BusinessRule`), con un mensaje que nombre el producto y el disponible, y MUST NOT reservar nada de ninguna línea.

#### Scenario: Reserva correcta
- **GIVEN** un producto con 20 unidades disponibles
- **WHEN** se crea un pedido de 3 unidades de ese producto
- **THEN** el disponible del producto pasa a 17

#### Scenario: Stock insuficiente en una línea
- **GIVEN** un producto A con 10 disponibles y un producto B con 2 disponibles
- **WHEN** se intenta crear un pedido de 5 de A y 3 de B
- **THEN** se obtiene `INSUFFICIENT_STOCK`, el mensaje nombra B y su disponible, y A sigue con 10 disponibles

#### Scenario: Producto sin stock
- **GIVEN** un producto con 0 disponibles
- **WHEN** se intenta crear un pedido de 1 unidad de ese producto
- **THEN** se obtiene `INSUFFICIENT_STOCK`

### Requirement: Liberación y consumo del stock reservado
Cancelar un pedido SHALL liberar las reservas de sus líneas, de modo que el disponible vuelve a su valor anterior. Enviar un pedido SHALL descontar de las existencias y de las reservas la cantidad de cada línea, sin cambiar el disponible.

#### Scenario: Cancelar libera el stock
- **GIVEN** un producto con 20 disponibles y un pedido en curso de 3 unidades que lo deja en 17
- **WHEN** se cancela el pedido
- **THEN** el disponible vuelve a 20

#### Scenario: Enviar consume el stock
- **GIVEN** un producto con 20 existencias, sin reservas, y un pedido en curso de 3 unidades
- **WHEN** se envía el pedido
- **THEN** el producto tiene 17 existencias, 0 reservas y 17 disponibles
