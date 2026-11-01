# Spec Delta

## MODIFIED Requirements

### Requirement: Proyectos de tests por capa
La solución SHALL contener los proyectos de tests `Sandbox.Domain.Tests`, `Sandbox.Application.Tests`, `Sandbox.Agents.Tests` y `Sandbox.Scenarios.Tests` bajo `tests/`. Cada uno SHALL incluir al menos un test que se ejecute con el comando de tests de la solución y pase.

#### Scenario: Cada proyecto de tests ejecuta al menos un test
- **GIVEN** la solución compilada
- **WHEN** se ejecutan los tests de la solución
- **THEN** se ejecuta al menos un test de cada uno de los cuatro proyectos de tests y todos pasan

#### Scenario: Los tests no necesitan red ni credenciales
- **GIVEN** un entorno sin acceso a red y sin claves de API configuradas
- **WHEN** se ejecutan los tests de la solución
- **THEN** todos los tests pasan
