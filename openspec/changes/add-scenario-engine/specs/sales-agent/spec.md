# Spec Delta

## REMOVED Requirements

### Requirement: Escenarios con nombre
**Reason**: Los escenarios pasan a ser una capacidad propia, `agent-scenarios`, que añade a cada uno su guion, sus comprobaciones y su veredicto. `sales-agent` queda para el agente en sí.
**Migration**: El requisito se mueve, con sus mismos escenarios, a "Escenarios con nombre" y "Definición de un escenario" de `agent-scenarios`. Los escenarios de cancelación se agrupan allí.

### Requirement: Pedidos previos de un escenario
**Reason**: Se mueve a `agent-scenarios`, junto al resto de lo que fija un escenario.
**Migration**: Mismo texto y mismo escenario en "Pedidos previos de un escenario" de `agent-scenarios`.

### Requirement: Escenario de cancelación
**Reason**: Se mueve a `agent-scenarios`, que reúne la lista de escenarios del catálogo.
**Migration**: `cancel-order` aparece en "Escenarios con nombre" de `agent-scenarios`, con los escenarios "Cancelación aprobada" y "Cancelación denegada", y sus comprobaciones en "Comprobaciones de la cancelación".
