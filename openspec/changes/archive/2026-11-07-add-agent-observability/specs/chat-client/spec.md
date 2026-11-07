## MODIFIED Requirements

### Requirement: Errores del proveedor sin filtrar la clave
Un error del proveedor SHALL presentarse y registrarse con su tipo y su código HTTP. El texto del error devuelto por el proveedor MUST NOT mostrarse, registrarse ni incluirse en la telemetría, porque puede contener fragmentos de la API key.

#### Scenario: Clave rechazada
- **GIVEN** una API key que el proveedor rechaza con HTTP 401, y cuyo mensaje de error repite parte de la clave
- **WHEN** se envía un mensaje
- **THEN** el error mostrado indica que la autenticación falló y el código 401, y ni la salida ni los registros contienen ningún fragmento de la clave

#### Scenario: Clave rechazada con telemetría
- **GIVEN** una API key que el proveedor rechaza con HTTP 401, cuyo mensaje de error repite parte de la clave, y un oyente que captura los spans
- **WHEN** se envía un mensaje
- **THEN** el span de la llamada está marcado como error, y ni su estado, ni sus atributos, ni sus eventos contienen ningún fragmento de la clave ni el texto de error del proveedor
