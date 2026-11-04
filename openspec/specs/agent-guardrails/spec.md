# agent-guardrails Specification

## Purpose
Define los guardrails deterministas del bucle del agente de ventas: cuánto puede trabajar una ejecución (llamadas al modelo, errores de tool seguidos y tokens), qué tools puede usar, cómo termina cuando un guardrail la para y qué deja en la auditoría. Ningún guardrail depende del modelo para decidir.

## Requirements

### Requirement: Configuración de los guardrails
Los guardrails SHALL leerse de la sección `Guardrails`: `MaxModelCalls` (por defecto 10), `MaxConsecutiveToolErrors` (por defecto 3), `TokenBudget` (por defecto 20.000) y `AllowedTools` (por defecto, las seis tools de `agent-tools`). Un límite menor que 1, una lista de tools vacía o una tool que no existe MUST detectarse al arrancar, sin llamar al modelo, y terminar con el código 2 y un mensaje que nombre el ajuste.

#### Scenario: Valores por defecto
- **GIVEN** una configuración sin sección `Guardrails`
- **WHEN** arranca el Runner con `--scenario happy-order`
- **THEN** la ejecución admite 10 llamadas al modelo, 3 errores de tool seguidos y 20.000 tokens, y se ofrecen al modelo las seis tools

#### Scenario: Límite inválido
- **GIVEN** `Guardrails:MaxModelCalls` igual a 0
- **WHEN** arranca el Runner con `--scenario happy-order`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra `Guardrails:MaxModelCalls`

#### Scenario: Tool permitida que no existe
- **GIVEN** `Guardrails:AllowedTools` con `ship_order`
- **WHEN** arranca el Runner con `--scenario happy-order`
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra `Guardrails:AllowedTools`

### Requirement: Límite de llamadas al modelo
Una ejecución MUST NOT llamar al modelo más de `MaxModelCalls` veces. Si al alcanzar el límite el modelo todavía pide tools, la ejecución SHALL ejecutar las pedidas en esa última respuesta y terminar sin volver a llamar al modelo, con el motivo de parada `ModelCallLimit`.

#### Scenario: Modelo que no deja de pedir tools
- **GIVEN** `MaxModelCalls` igual a 3 y un modelo que en cada respuesta pide `list_products`
- **WHEN** el agente atiende la petición
- **THEN** el modelo recibe exactamente 3 llamadas, `list_products` se ejecuta 3 veces y el motivo de parada es `ModelCallLimit`

#### Scenario: Respuesta en la última llamada permitida
- **GIVEN** `MaxModelCalls` igual a 3 y un modelo que pide `list_products` dos veces y en la tercera llamada responde con texto
- **WHEN** el agente atiende la petición
- **THEN** la respuesta final es ese texto y el motivo de parada es `Completed`

### Requirement: Límite de errores de tool seguidos
Cuando `MaxConsecutiveToolErrors` invocaciones de tools seguidas terminan con un error, de cualquier código e incluido `BLOCKED_BY_POLICY`, la ejecución SHALL terminar tras la última de ellas, sin volver a llamar al modelo, con el motivo de parada `ToolErrorLimit`. Una invocación con éxito SHALL reiniciar la cuenta.

#### Scenario: Tres errores seguidos
- **GIVEN** `MaxConsecutiveToolErrors` igual a 3 y un modelo que pide `get_order` de `ORD-9999` en cada respuesta
- **WHEN** el agente atiende la petición
- **THEN** `get_order` devuelve `ORDER_NOT_FOUND` 3 veces, el modelo recibe 3 llamadas y el motivo de parada es `ToolErrorLimit`

#### Scenario: Un éxito reinicia la cuenta
- **GIVEN** `MaxConsecutiveToolErrors` igual a 3 y un modelo que pide, en respuestas sucesivas, `get_order` de `ORD-9999` dos veces, `list_products`, `get_order` de `ORD-9999` dos veces y después responde con texto
- **WHEN** el agente atiende la petición
- **THEN** el motivo de parada es `Completed`

### Requirement: Presupuesto de tokens
Antes de cada llamada al modelo, si la suma de los tokens de entrada y de salida de las llamadas anteriores de la ejecución alcanza `TokenBudget`, la llamada MUST NOT hacerse y la ejecución SHALL terminar con el motivo de parada `TokenBudget`. Una llamada ya iniciada no se interrumpe, así que el total puede superar el presupuesto en lo que consuma la última.

#### Scenario: Presupuesto agotado
- **GIVEN** `TokenBudget` igual a 300 y un modelo que pide `list_products` en cada respuesta y gasta 100 tokens de entrada y 50 de salida por llamada
- **WHEN** el agente atiende la petición
- **THEN** el modelo recibe 2 llamadas, el resultado indica 200 tokens de entrada y 100 de salida, y el motivo de parada es `TokenBudget`

#### Scenario: Ejecución dentro del presupuesto
- **GIVEN** el presupuesto por defecto y el modelo falso del escenario feliz
- **WHEN** el agente atiende la petición
- **THEN** el motivo de parada es `Completed`

### Requirement: Allow-list de tools
Al modelo SHALL ofrecérsele solo las tools permitidas: las de `AllowedTools`, reducidas a las del escenario cuando este fija las suyas. Si el modelo pide igualmente una tool del catálogo que no está permitida, MUST NOT ejecutarse, y el modelo SHALL recibir un `ToolError` con el código `BLOCKED_BY_POLICY`, no reintentable y con un mensaje que no mencione la configuración.

#### Scenario: Solo se ofrecen las permitidas
- **GIVEN** `AllowedTools` con `list_products` y `get_stock`
- **WHEN** se inspecciona la lista de tools que recibe el modelo
- **THEN** contiene exactamente `list_products` y `get_stock`

#### Scenario: Tool no permitida pedida igualmente
- **GIVEN** `AllowedTools` sin `place_order` y un modelo que pide `place_order` con líneas válidas y luego responde con texto
- **WHEN** el agente atiende la petición
- **THEN** el modelo recibe `BLOCKED_BY_POLICY` no reintentable, no se crea ningún pedido, el stock no cambia y el resultado lista `place_order` con `BLOCKED_BY_POLICY`

### Requirement: Tools desconocidas
Si el modelo pide una tool que no existe en el catálogo de `agent-tools`, MUST NOT ejecutarse nada y el bucle SHALL continuar. La petición SHALL aparecer en el resultado de la ejecución y en la auditoría con el resultado `UNKNOWN_TOOL`, con el nombre pedido recortado a 64 caracteres.

#### Scenario: Nombre inventado
- **GIVEN** un modelo que pide `ship_order` y en la siguiente llamada responde con texto
- **WHEN** el agente atiende la petición
- **THEN** el modelo recibe 2 llamadas, ningún pedido cambia, el resultado lista `ship_order` con `UNKNOWN_TOOL`, la auditoría contiene un evento con `ship_order` y el motivo de parada es `Completed`

#### Scenario: Nombre demasiado largo
- **GIVEN** un modelo que pide una tool con un nombre de 200 caracteres
- **WHEN** se revisa la auditoría
- **THEN** el nombre registrado tiene 64 caracteres

### Requirement: Motivo de parada
Cada ejecución SHALL terminar con un motivo de parada: `Completed` cuando el modelo responde con texto sin pedir tools, o el del guardrail que la paró. Si la para un guardrail, la respuesta final SHALL ser un texto fijo del sistema, en español, que diga que la petición no se pudo completar, y MUST NOT ser texto del modelo. La parada SHALL dejar un evento de auditoría con su motivo.

#### Scenario: Parada por errores seguidos
- **GIVEN** un modelo que acompaña cada petición de `get_order` de `ORD-9999` con el texto "Lo busco"
- **WHEN** la ejecución termina por `ToolErrorLimit`
- **THEN** la respuesta final es el texto fijo del sistema, no contiene "Lo busco", y la auditoría contiene un evento de parada con `ToolErrorLimit`

#### Scenario: Ejecución completada
- **GIVEN** el modelo falso del escenario feliz
- **WHEN** termina la ejecución
- **THEN** el motivo de parada es `Completed` y la respuesta final es el último texto del modelo

### Requirement: Longitud máxima de la petición
El agente MUST NOT llamar al modelo con una petición de más de 2.000 caracteres: la SHALL rechazar antes de cualquier llamada.

#### Scenario: Petición demasiado larga
- **GIVEN** una petición de 2.001 caracteres
- **WHEN** se pide al agente que la atienda
- **THEN** la rechaza y el modelo no recibe ninguna llamada

### Requirement: Auditoría estructurada
Los eventos de auditoría SHALL registrarse en una categoría de registro propia, distinta de la de los registros operativos, con un identificador de evento estable por tipo. Cada evento SHALL llevar el identificador de la ejecución, distinto en cada una, y el cliente de sesión. Los eventos de invocación de tools MUST NOT incluir sus argumentos ni los datos de sus resultados, y ningún evento MUST incluir el texto de la petición ni secretos.

#### Scenario: Correlación de una ejecución
- **GIVEN** una ejecución en la que el modelo invoca `list_products` y `get_order` de un pedido de otro cliente
- **WHEN** se revisa la auditoría
- **THEN** los eventos de las dos invocaciones y el de la denegación llevan el mismo identificador de ejecución y el cliente de sesión

#### Scenario: Dos ejecuciones
- **GIVEN** dos ejecuciones seguidas del agente
- **WHEN** se revisa la auditoría
- **THEN** sus eventos llevan identificadores de ejecución distintos

#### Scenario: Sin datos de la petición
- **GIVEN** una ejecución con la petición "quiero dos tazas rojas" que invoca `place_order`
- **WHEN** se revisa la auditoría
- **THEN** ningún evento contiene el texto de la petición, el SKU, la cantidad ni la clave de idempotencia
