## ADDED Requirements

### Requirement: Pedidos previos de un escenario
Un escenario SHALL poder fijar pedidos que existen antes de atender la petición, cada uno con su cliente y sus líneas. Esos pedidos SHALL crearse al arrancar, con las reglas de `order-operations` y sin pasar por el inyector de fallos, y MUST NOT contar como llamadas al modelo ni como tools invocadas.

#### Scenario: Fallos inducidos intactos
- **GIVEN** un escenario con un pedido previo y una regla del inyector que hace fallar la primera creación de pedido
- **WHEN** arranca el escenario
- **THEN** el pedido previo existe y la regla sigue sin consumirse

### Requirement: Escenario de cancelación
El agente SHALL ofrecer el escenario `cancel-order`: el cliente `CUST-001` ya tiene el pedido `ORD-0001` con dos tazas rojas y pide cancelarlo.

#### Scenario: Cancelación aprobada
- **GIVEN** el escenario `cancel-order`, un aprobador que aprueba y un modelo falso que pide `cancel_order` de `ORD-0001` y luego responde
- **WHEN** se ejecuta el escenario
- **THEN** `ORD-0001` queda en `Cancelled` y el disponible de `SKU-MUG-RED` vuelve a 20

#### Scenario: Cancelación denegada
- **GIVEN** el escenario `cancel-order`, un aprobador que deniega y un modelo falso que pide `cancel_order` de `ORD-0001` y luego responde
- **WHEN** se ejecuta el escenario
- **THEN** `ORD-0001` sigue en `Placed`, el disponible de `SKU-MUG-RED` sigue en 18 y el código de salida es 0
