# fault-injection Specification

## Purpose
Define cómo se inducen fallos y latencia en las operaciones de aplicación de forma configurable y reproducible, para probar cómo reaccionan el agente y el código ante errores transitorios, respuestas perdidas y lentitud, sin depender del azar ni de servicios reales.

## Requirements

### Requirement: Desactivado por defecto
Sin reglas configuradas, el inyector SHALL dejar pasar todas las operaciones sin cambios, sin errores añadidos y sin retraso.

#### Scenario: Sin reglas
- **GIVEN** una configuración del inyector sin reglas
- **WHEN** se ejecutan commands y queries
- **THEN** los resultados y el estado son los mismos que sin inyector, y ninguna llamada espera

### Requirement: Reglas por operación
Cada regla SHALL aplicarse a una operación concreta (`PlaceOrder`, `CancelOrder`, `ShipOrder`, `GetOrder`, `GetCustomer` o `GetStock`) o a todas con `*`. Una regla MUST NOT afectar a las operaciones que no nombra.

#### Scenario: Regla sobre una sola operación
- **GIVEN** una regla que hace fallar siempre `GetStock`
- **WHEN** se ejecutan `GetStock` y `GetOrder`
- **THEN** `GetStock` falla y `GetOrder` responde con normalidad

#### Scenario: Regla sobre todas las operaciones
- **GIVEN** una regla con operación `*` que falla siempre
- **WHEN** se ejecuta cualquier operación
- **THEN** falla

### Requirement: Disparador siempre
Una regla con disparador `Always` SHALL fallar en todas las llamadas a su operación.

#### Scenario: Fallo permanente
- **GIVEN** una regla `Always` sobre `PlaceOrder`
- **WHEN** se ejecuta `PlaceOrder` tres veces
- **THEN** las tres llamadas fallan

### Requirement: Disparador por número de llamadas
Una regla con disparador `FirstCalls` y un número N SHALL fallar en las N primeras llamadas a su operación y dejar pasar las siguientes. El recuento SHALL ser propio de cada regla.

#### Scenario: Fallan las dos primeras
- **GIVEN** una regla `FirstCalls` con N = 2 sobre `GetOrder`
- **WHEN** se ejecuta `GetOrder` cuatro veces
- **THEN** fallan la primera y la segunda, y la tercera y la cuarta responden con normalidad

### Requirement: Disparador probabilístico con semilla
Una regla `Probability` con probabilidad p SHALL fallar en cada llamada con esa probabilidad, usando una semilla de la configuración. Con la misma semilla, configuración y secuencia de llamadas, el patrón de fallos SHALL repetirse en el mismo runtime de .NET. Las llamadas a operaciones que no cubre la regla MUST NOT alterar su patrón.

#### Scenario: Misma semilla, mismo patrón
- **GIVEN** dos inyectores con la misma semilla y una regla `Probability` con p = 0,5 sobre `GetStock`
- **WHEN** se ejecuta `GetStock` 20 veces en cada uno
- **THEN** los dos fallan exactamente en las mismas llamadas

#### Scenario: Otras operaciones no alteran el patrón
- **GIVEN** dos inyectores con la misma semilla y la misma regla `Probability` sobre `GetStock`
- **WHEN** en uno se intercalan llamadas a `GetOrder` entre las de `GetStock` y en el otro no
- **THEN** los dos fallan en las mismas llamadas a `GetStock`

#### Scenario: Probabilidades extremas
- **GIVEN** un inyector con una regla de p = 0 y otro con una regla de p = 1, sobre la misma operación
- **WHEN** se ejecuta su operación 20 veces
- **THEN** con p = 0 no falla ninguna, y con p = 1 fallan todas

### Requirement: Error transitorio inducido
Un fallo inducido SHALL devolverse como resultado de error con código `SERVICE_UNAVAILABLE` y tipo `Transient`, igual en todos los momentos de fallo, y MUST NOT lanzarse como excepción.

#### Scenario: Código del fallo inducido
- **GIVEN** una regla `Always` sobre `GetCustomer`
- **WHEN** se ejecuta `GetCustomer`
- **THEN** se obtiene `SERVICE_UNAVAILABLE` con tipo `Transient`

### Requirement: Fallo antes de ejecutar
Una regla con momento `Before` SHALL fallar sin ejecutar la operación, de modo que el estado no cambia.

#### Scenario: PlaceOrder falla antes
- **GIVEN** una regla `FirstCalls` con N = 1 y momento `Before` sobre `PlaceOrder`
- **WHEN** se ejecuta `PlaceOrder` con la clave `k1`
- **THEN** se obtiene `SERVICE_UNAVAILABLE`, no se crea ningún pedido y el stock no cambia

### Requirement: Fallo después de ejecutar
Una regla con momento `After` SHALL ejecutar la operación, conservar todos sus efectos (incluido el registro de la clave de idempotencia) y devolver al llamante el error inducido en lugar del resultado.

#### Scenario: Respuesta perdida y reintento idempotente
- **GIVEN** una regla `FirstCalls` con N = 1 y momento `After` sobre `CancelOrder`, y un pedido en `Placed`
- **WHEN** se ejecuta `CancelOrder` con la clave `k1` y después se repite con `k1`
- **THEN** la primera llamada devuelve `SERVICE_UNAVAILABLE` aunque el pedido ya está en `Cancelled` y su stock liberado, y la segunda devuelve con éxito la vista del pedido cancelado

### Requirement: Latencia inducida
Una regla MAY añadir una latencia que SHALL esperarse antes de ejecutar la operación, medida con el reloj inyectado, falle o no la regla. Si la llamada se cancela durante la espera, la cancelación MUST propagarse sin efectos.

#### Scenario: Latencia sin fallo
- **GIVEN** una regla que solo añade 200 ms a `GetOrder`, con un reloj falso
- **WHEN** se ejecuta `GetOrder`
- **THEN** la llamada no termina hasta que el reloj avanza 200 ms, y después responde con normalidad

#### Scenario: Cancelación durante la latencia
- **GIVEN** una regla que añade 5 s a `PlaceOrder`, con un reloj falso
- **WHEN** se cancela la llamada antes de que el reloj avance
- **THEN** la llamada termina por cancelación y no se crea ningún pedido

### Requirement: Combinación de reglas
Todas las reglas que cubren una operación SHALL evaluarse en cada llamada, en el orden en que se declaran. Sus latencias se suman, y el momento del fallo lo decide la primera regla que falla. El recuento y la secuencia aleatoria de cada regla avanzan en cada llamada que cubre, falle o no otra regla.

#### Scenario: Latencia global y fallo concreto
- **GIVEN** una regla `*` que añade 100 ms y otra `Always` sobre `ShipOrder`, en ese orden
- **WHEN** se ejecuta `ShipOrder`
- **THEN** la llamada espera 100 ms y devuelve `SERVICE_UNAVAILABLE`

### Requirement: Validación de la configuración del inyector
La configuración SHALL rechazarse si una regla nombra una operación desconocida, si `FirstCalls` no tiene un número de al menos 1, si `Probability` queda fuera de 0 a 1, si la latencia es negativa o supera 30 s, o si la regla ni falla ni añade latencia. El error MUST nombrar la regla y el ajuste inválidos.

#### Scenario: Operación desconocida
- **GIVEN** una regla sobre la operación `DeleteEverything`
- **WHEN** se valida la configuración
- **THEN** se rechaza y el error nombra esa regla y su ajuste de operación

#### Scenario: Probabilidad fuera de rango
- **GIVEN** una regla `Probability` con p = 1,5
- **WHEN** se valida la configuración
- **THEN** se rechaza y el error nombra el ajuste de probabilidad de esa regla

### Requirement: Registro de los fallos inducidos
Cada fallo o latencia inducidos SHALL registrarse con la operación, el número de la regla, el momento y la latencia aplicada. El registro MUST NOT incluir los argumentos de la operación.

#### Scenario: Traza de un fallo inducido
- **GIVEN** una regla `Always` con momento `After` sobre `PlaceOrder`
- **WHEN** se ejecuta `PlaceOrder`
- **THEN** el registro contiene una entrada con `PlaceOrder`, la regla 0 y el momento `After`, sin el cliente ni las líneas
