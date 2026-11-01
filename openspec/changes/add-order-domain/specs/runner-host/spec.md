# Spec Delta

## ADDED Requirements

### Requirement: Servicios de aplicación registrados al arrancar
El Runner SHALL registrar las operaciones de aplicación sobre el catálogo semilla, con el inyector de fallos configurado desde la sección `FaultInjection`. Sin esa sección, SHALL arrancar sin fallos inducidos. Una configuración del inyector inválida MUST detectarse al arrancar y terminar con el código 2, con un mensaje que nombre el ajuste inválido.

#### Scenario: Sin sección del inyector
- **GIVEN** una configuración válida de DeepSeek y ninguna sección `FaultInjection`
- **WHEN** se ejecuta el Runner con el cliente falso
- **THEN** termina con código 0 como en el saludo de comprobación

#### Scenario: Configuración del inyector inválida
- **GIVEN** una configuración válida de DeepSeek y una regla del inyector con probabilidad 1,5
- **WHEN** se ejecuta el Runner
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra el ajuste de probabilidad de esa regla

#### Scenario: Varias secciones inválidas a la vez
- **GIVEN** ninguna API key y una regla del inyector con una operación desconocida
- **WHEN** se ejecuta el Runner
- **THEN** termina con código 2, y el mensaje nombra la opción de la clave y el ajuste de operación de esa regla

#### Scenario: Operaciones disponibles en el contenedor de servicios
- **GIVEN** el host del Runner construido con la configuración por defecto
- **WHEN** se piden al contenedor de servicios los commands y las queries
- **THEN** se obtienen, y una query del cliente bloqueado del catálogo semilla devuelve su vista
