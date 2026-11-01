# solution-layout Specification

## Purpose
Define la estructura de proyectos de la solución y la dirección permitida de las dependencias entre ellos, de modo que la separación entre dominio, aplicación e invocación del LLM sea verificable de forma automática desde el primer día.

## Requirements

### Requirement: Proyectos de código por capa
La solución SHALL contener los proyectos de código `Sandbox.Domain`, `Sandbox.Application`, `Sandbox.Agents` y `Sandbox.Runner` bajo `src/`. `Sandbox.Runner` SHALL ser una aplicación de consola ejecutable y los demás SHALL ser bibliotecas.

#### Scenario: La solución incluye las cuatro capas
- **GIVEN** un clon limpio del repositorio
- **WHEN** se listan los proyectos de la solución
- **THEN** aparecen `Sandbox.Domain`, `Sandbox.Application`, `Sandbox.Agents` y `Sandbox.Runner`, todos bajo `src/`

#### Scenario: El Runner es ejecutable
- **GIVEN** la solución compilada
- **WHEN** se inspecciona el tipo de salida de cada proyecto de código
- **THEN** solo `Sandbox.Runner` produce una aplicación ejecutable

### Requirement: Dirección de dependencias entre capas
Las referencias entre proyectos de código SHALL seguir una sola dirección. `Sandbox.Domain` SHALL NOT referenciar ningún otro proyecto. `Sandbox.Application` SHALL referenciar solo a `Sandbox.Domain`. `Sandbox.Agents` SHALL referenciar solo a `Sandbox.Application` y `Sandbox.Domain`. `Sandbox.Runner` MAY referenciar a todos.

#### Scenario: El dominio no referencia proyectos
- **GIVEN** la solución en su estado final del change
- **WHEN** se listan las referencias de proyecto de `Sandbox.Domain`
- **THEN** la lista está vacía

#### Scenario: Una capa inferior no referencia a una superior
- **GIVEN** la solución en su estado final del change
- **WHEN** se listan las referencias de proyecto de `Sandbox.Application`
- **THEN** solo aparece `Sandbox.Domain`, y no `Sandbox.Agents` ni `Sandbox.Runner`

#### Scenario: Una referencia circular impide compilar
- **GIVEN** una referencia añadida de `Sandbox.Domain` hacia `Sandbox.Application`
- **WHEN** se compila la solución
- **THEN** el build falla

### Requirement: Dominio y aplicación libres de librerías de IA
`Sandbox.Domain` y `Sandbox.Application` SHALL NOT depender, directa ni transitivamente, de librerías de IA (en particular `Microsoft.Extensions.AI` y los SDK de proveedores de modelos). Una prueba automática SHALL detectar el incumplimiento. Las librerías de IA SHALL limitarse a `Sandbox.Agents` y `Sandbox.Runner`.

#### Scenario: El dominio no referencia librerías de IA
- **GIVEN** el ensamblado de `Sandbox.Domain`
- **WHEN** se ejecuta la prueba de reglas de capas
- **THEN** ninguno de sus ensamblados referenciados pertenece a una librería de IA y la prueba pasa

#### Scenario: La aplicación no referencia librerías de IA
- **GIVEN** el ensamblado de `Sandbox.Application`
- **WHEN** se ejecuta la prueba de reglas de capas
- **THEN** ninguno de sus ensamblados referenciados pertenece a una librería de IA y la prueba pasa

#### Scenario: Añadir una librería de IA al dominio rompe la prueba
- **GIVEN** una referencia a una librería de IA añadida a `Sandbox.Domain` y usada en código
- **WHEN** se ejecutan los tests de la solución
- **THEN** la prueba de reglas de capas falla e identifica el ensamblado prohibido

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
