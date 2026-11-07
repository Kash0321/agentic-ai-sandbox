# Spec Delta

## ADDED Requirements

### Requirement: Lectura de inspección
La aplicación SHALL ofrecer una lectura de inspección del estado completo, sin filtrar por cliente: cada pedido con su identificador, su cliente, su estado y sus líneas, y cada producto con sus existencias, lo reservado y el disponible. MUST NOT cambiar ningún estado ni consumir números de pedido, y MUST NOT ofrecerse al modelo como tool: sirve para evaluar escenarios, no para atender al cliente.

#### Scenario: Foto con pedidos de dos clientes
- **GIVEN** `ORD-0001` de `CUST-001` en `Placed` con 2 unidades de `SKU-MUG-RED` y `ORD-0002` de `CUST-002` cancelado
- **WHEN** se hace la lectura de inspección
- **THEN** contiene los dos pedidos con su cliente y su estado, y `SKU-MUG-RED` con 20 existencias, 2 reservadas y 18 disponibles

#### Scenario: Sin efectos
- **GIVEN** un estado con pedidos y reservas
- **WHEN** se hace la lectura de inspección varias veces
- **THEN** los pedidos, el stock y el siguiente número de pedido siguen iguales

#### Scenario: Fuera del catálogo de tools
- **GIVEN** el catálogo de tools del agente
- **WHEN** se revisan sus nombres
- **THEN** ninguna tool da acceso a la lectura de inspección
