# Spec Delta

## MODIFIED Requirements

### Requirement: Reglas por operación
Cada regla SHALL aplicarse a una operación concreta (`PlaceOrder`, `CancelOrder`, `ShipOrder`, `GetOrder`, `GetCustomer`, `GetStock` o `ListProducts`) o a todas con `*`. Una regla MUST NOT afectar a las operaciones que no nombra.

#### Scenario: Regla sobre una sola operación
- **GIVEN** una regla que hace fallar siempre `GetStock`
- **WHEN** se ejecutan `GetStock` y `GetOrder`
- **THEN** `GetStock` falla y `GetOrder` responde con normalidad

#### Scenario: Regla sobre todas las operaciones
- **GIVEN** una regla con operación `*` que falla siempre
- **WHEN** se ejecuta cualquier operación
- **THEN** falla

#### Scenario: Regla sobre el listado de productos
- **GIVEN** una regla que hace fallar siempre `ListProducts`
- **WHEN** se ejecutan `ListProducts` y `GetStock`
- **THEN** `ListProducts` falla con `SERVICE_UNAVAILABLE` y `GetStock` responde con normalidad
