# Spec Delta

## MODIFIED Requirements

### Requirement: Opciones de conexión validadas al arrancar
La solución SHALL leer de la configuración el endpoint, el modelo, el modo de razonamiento y la API key del proveedor, y SHALL validarlos al arrancar, antes de hacer ninguna llamada. Una opción ausente o inválida MUST impedir el arranque con un error que nombra la opción y no muestra su valor. Con el modelo con guion o con la suborden `list` del Runner, la API key MUST NOT exigirse y el cliente del proveedor MUST NOT construirse.

#### Scenario: Falta la API key
- **GIVEN** una configuración sin API key
- **WHEN** arranca el Runner
- **THEN** el arranque falla antes de llamar al modelo, el error nombra la opción de la API key y no se hace ninguna petición de red

#### Scenario: Endpoint inválido
- **GIVEN** una configuración cuyo endpoint no es una URL absoluta HTTPS
- **WHEN** arranca el Runner
- **THEN** el arranque falla y el error nombra la opción del endpoint

#### Scenario: Opciones válidas
- **GIVEN** una configuración con endpoint, modelo y API key válidos
- **WHEN** arranca el Runner
- **THEN** la validación pasa y el cliente queda disponible por inyección de dependencias

#### Scenario: Modelo con guion sin clave
- **GIVEN** una configuración sin API key
- **WHEN** arranca el Runner con `--scenario happy-order --model scripted`
- **THEN** el arranque no falla y no se construye el cliente del proveedor
