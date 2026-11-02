# Spec Delta

## ADDED Requirements

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
