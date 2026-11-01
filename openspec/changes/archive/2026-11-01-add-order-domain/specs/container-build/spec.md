# Spec Delta

## MODIFIED Requirements

### Requirement: Objetivo de tests en contenedor
El repositorio SHALL proporcionar una definición de imagen por etapas que restaura, compila y ejecuta todos los tests de la solución en una etapa que se puede construir por separado. Si algún test falla, la construcción de esa etapa MUST fallar.

#### Scenario: Tests en verde
- **GIVEN** un clon del repositorio con todos los tests en verde
- **WHEN** se construye la etapa de tests (`docker build --target test .`)
- **THEN** la construcción termina correctamente tras ejecutar los tests de todos los proyectos de tests de la solución

#### Scenario: Un test en rojo rompe la construcción
- **GIVEN** un test modificado para que falle
- **WHEN** se construye la etapa de tests
- **THEN** la construcción falla y el resultado identifica el test fallido
