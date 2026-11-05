# sales-agent Specification

## Purpose
Define el agente de ventas: cómo atiende la petición de un cliente usando las tools sobre el dominio, qué instrucciones recibe el modelo, qué resultado produce cada ejecución y qué escenarios con nombre existen para ejecutarlo de forma reproducible. El agente orquesta; las reglas de negocio las decide el dominio.

## Requirements

### Requirement: Bucle de invocación de tools
Ante una petición del cliente, el agente SHALL enviar al modelo las instrucciones del sistema, la petición y las tools permitidas de `agent-tools`. Cada vez que el modelo pida invocar tools, SHALL ejecutarlas y devolverle sus resultados, hasta que el modelo responda con texto sin pedir más tools o hasta que un guardrail de `agent-guardrails` pare la ejecución. Las tools de una misma respuesta SHALL ejecutarse en serie.

#### Scenario: Pedido en tres llamadas al modelo
- **GIVEN** un modelo que primero pide `list_products`, después pide `place_order` y por último responde con texto
- **WHEN** el agente atiende "quiero dos tazas rojas"
- **THEN** el modelo recibe el resultado de cada tool antes de la siguiente llamada, se crea un pedido y la ejecución termina tras la tercera llamada

#### Scenario: Varias tools en una misma respuesta
- **GIVEN** un modelo que en una sola respuesta pide `list_products` y `get_customer`
- **WHEN** el agente atiende la petición
- **THEN** se ejecutan las dos y el modelo recibe los dos resultados en la siguiente llamada

#### Scenario: Parada por un guardrail
- **GIVEN** `Guardrails:MaxModelCalls` igual a 2 y un modelo que en cada respuesta pide `list_products`
- **WHEN** el agente atiende la petición
- **THEN** la ejecución termina tras la segunda llamada, aunque el modelo no haya respondido con texto

### Requirement: Instrucciones del sistema sin reglas de negocio
Las instrucciones del sistema SHALL indicar al modelo:
- que actúa en nombre del cliente con el que habla;
- que use las tools para consultar y actuar, y no invente datos;
- que, al repetir una petición tras un error reintentable, reutilice la misma clave de idempotencia;
- que responda en español.

Las instrucciones MUST NOT contener reglas ni límites de negocio (cantidades, importes, estados permitidos), que decide el dominio.

#### Scenario: Instrucciones enviadas al modelo
- **GIVEN** una ejecución del agente
- **WHEN** se inspecciona el primer mensaje enviado al modelo
- **THEN** es el mensaje del sistema, menciona la clave de idempotencia y no contiene el importe máximo ni el rango de cantidades del dominio

### Requirement: Resultado de una ejecución
Cada ejecución SHALL producir:
- la respuesta final: el texto del último mensaje del modelo o, si la paró un guardrail, el texto fijo del sistema;
- el motivo de parada de `agent-guardrails`;
- el número de llamadas al modelo;
- la lista de tools pedidas, en orden y con su resultado (éxito o código de error);
- el total de tokens de entrada y de salida.

Los textos que el modelo emita junto a sus peticiones de tools MUST NOT formar parte de la respuesta final; SHALL quedar en el registro.

#### Scenario: Textos intermedios
- **GIVEN** un modelo que acompaña su petición de `list_products` con el texto "Voy a buscar el producto" y termina con "Pedido ORD-0001 creado"
- **WHEN** termina la ejecución
- **THEN** la respuesta final es "Pedido ORD-0001 creado", el registro contiene "Voy a buscar el producto", y el resultado indica dos llamadas al modelo, la tool `list_products` con éxito y el motivo `Completed`

#### Scenario: Tokens acumulados
- **GIVEN** tres llamadas al modelo de 100, 200 y 300 tokens de entrada
- **WHEN** termina la ejecución
- **THEN** el resultado indica 600 tokens de entrada

#### Scenario: Resultado parcial tras una parada
- **GIVEN** un modelo que pide `get_order` de `ORD-9999` en cada respuesta, con 100 tokens de entrada por llamada
- **WHEN** la ejecución termina por `ToolErrorLimit`
- **THEN** el resultado indica 3 llamadas al modelo, 300 tokens de entrada y las tres invocaciones de `get_order` con `ORDER_NOT_FOUND`

### Requirement: Escenarios con nombre
El agente SHALL ofrecer estos escenarios reproducibles, cada uno con su cliente de sesión, su petición y, si procede, sus fallos inducidos:
- `happy-order`: el cliente `CUST-001` pide dos tazas rojas, sin fallos inducidos;
- `lost-response`: la misma petición, con la primera creación de pedido fallando después de ejecutarse (respuesta perdida);
- `read-only`: la misma petición, solo con tools de lectura.

Los fallos inducidos de un escenario SHALL sumarse a los configurados, no sustituirlos.

#### Scenario: Escenario feliz
- **GIVEN** el escenario `happy-order` y un modelo falso que pide `list_products`, después `place_order` con 2 unidades de `SKU-MUG-RED` y luego responde
- **WHEN** se ejecuta el escenario
- **THEN** existe `ORD-0001` en `Placed` a nombre de `CUST-001` y el disponible de `SKU-MUG-RED` pasa a 18

#### Scenario: Respuesta perdida
- **GIVEN** el escenario `lost-response` y un modelo falso que invoca `place_order` dos veces con la misma clave y las mismas líneas
- **WHEN** se ejecuta el escenario
- **THEN** la primera invocación devuelve `SERVICE_UNAVAILABLE` reintentable, la segunda devuelve `ORD-0001` con éxito, y no existe `ORD-0002`

#### Scenario: Solo lectura
- **GIVEN** el escenario `read-only` y un modelo falso que pide `list_products`, después `place_order` y luego responde
- **WHEN** se ejecuta el escenario
- **THEN** el modelo recibe solo `list_products`, `get_stock`, `get_customer` y `get_order`, `place_order` devuelve `BLOCKED_BY_POLICY`, no existe ningún pedido y el disponible de `SKU-MUG-RED` sigue en 20

#### Scenario: El escenario no amplía la allow-list
- **GIVEN** `Guardrails:AllowedTools` con solo `list_products`
- **WHEN** se ejecuta el escenario `read-only`
- **THEN** el modelo recibe solo `list_products`

### Requirement: Aviso de razonamiento con tools
Si el razonamiento del modelo está activado, el agente SHALL registrar al empezar cada ejecución un aviso: el proveedor documenta que el razonamiento debe reenviarse en los turnos con tools, y esta solución no lo reenvía. La ejecución SHALL continuar.

#### Scenario: Razonamiento activado
- **GIVEN** la configuración con el razonamiento activado
- **WHEN** se ejecuta el agente
- **THEN** el registro contiene el aviso y la ejecución termina con normalidad

#### Scenario: Razonamiento desactivado
- **GIVEN** la configuración por defecto
- **WHEN** se ejecuta el agente
- **THEN** no se registra el aviso
