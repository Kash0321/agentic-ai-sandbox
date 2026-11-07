# agent-observability Specification

## Purpose
Define la telemetría del agente y del Runner con OpenTelemetry: qué trazas, logs y métricas se emiten en cada ejecución, cuándo se exportan por OTLP, qué datos llevan y cuáles no, y cómo se activa la captura opcional del contenido de las conversaciones.

## Requirements

### Requirement: Exportación solo con endpoint configurado
El Runner SHALL exportar telemetría por OTLP solo cuando la opción `OTEL_EXPORTER_OTLP_ENDPOINT` tenga un valor no vacío. Sin valor, o con un valor vacío, MUST NOT registrar ningún exportador ni intentar conexiones de telemetría. La exportación MUST NOT cambiar la salida estándar, el resultado de la ejecución ni el código de salida.

#### Scenario: Sin endpoint
- **GIVEN** una configuración sin `OTEL_EXPORTER_OTLP_ENDPOINT` y un cliente de chat falso
- **WHEN** se ejecuta el Runner con `--scenario happy-order`
- **THEN** termina con código 0, el host no tiene ningún exportador de telemetría registrado y no se intenta ninguna conexión de telemetría

#### Scenario: Endpoint vacío
- **GIVEN** `OTEL_EXPORTER_OTLP_ENDPOINT` definida con un valor vacío, como la deja compose por defecto
- **WHEN** arranca el Runner
- **THEN** se comporta como sin endpoint

#### Scenario: Misma salida con y sin exportación
- **GIVEN** el mismo cliente de chat falso con guion
- **WHEN** se ejecuta `--scenario happy-order` sin endpoint y con un endpoint válido
- **THEN** la salida estándar y el código de salida son iguales en las dos ejecuciones

### Requirement: Endpoint validado al arrancar
Un valor no vacío de `OTEL_EXPORTER_OTLP_ENDPOINT` SHALL ser una URL absoluta con esquema `http` o `https`. Un valor inválido MUST detectarse al arrancar, sin llamar al modelo, y terminar con el código 2 y un mensaje que nombre la opción.

#### Scenario: Endpoint que no es una URL
- **GIVEN** `OTEL_EXPORTER_OTLP_ENDPOINT` igual a `dashboard:18889`
- **WHEN** se ejecuta el Runner
- **THEN** termina con código 2 sin llamar al modelo, y el mensaje nombra `OTEL_EXPORTER_OTLP_ENDPOINT`

#### Scenario: Esquema no admitido
- **GIVEN** `OTEL_EXPORTER_OTLP_ENDPOINT` igual a `ftp://localhost:4317`
- **WHEN** se ejecuta el Runner
- **THEN** termina con código 2 y el mensaje nombra `OTEL_EXPORTER_OTLP_ENDPOINT`

### Requirement: Aviso de telemetría activa
Cuando la exportación está activa, el Runner SHALL registrar al arrancar, por la salida de errores, una línea que indique el endpoint de destino. Sin exportación, MUST NOT registrarla.

#### Scenario: Arranque con endpoint
- **GIVEN** `OTEL_EXPORTER_OTLP_ENDPOINT` igual a `http://localhost:4317`
- **WHEN** arranca el Runner
- **THEN** el registro contiene una línea que dice que la telemetría está activa hacia `http://localhost:4317`

#### Scenario: Arranque sin endpoint
- **GIVEN** una configuración sin `OTEL_EXPORTER_OTLP_ENDPOINT`
- **WHEN** arranca el Runner
- **THEN** el registro no contiene esa línea

### Requirement: Señales exportadas
Con la exportación activa, el Runner SHALL exportar las trazas del agente y del modelo, los registros (incluida la auditoría) y las métricas de duración y tokens de las llamadas al modelo, con el nombre de servicio `sandbox-runner`. Cada registro SHALL llevar el mensaje ya formateado, sus propiedades y las de sus *scopes*.

#### Scenario: Auditoría exportada y correlacionada
- **GIVEN** la exportación activa y una ejecución que invoca `list_products`
- **WHEN** se revisan los registros exportados
- **THEN** el evento `ToolInvoked` aparece con su mensaje formateado, con `RunId` y `CustomerId` entre sus propiedades y con el identificador de traza de esa ejecución

#### Scenario: Recurso del servicio
- **GIVEN** la exportación activa
- **WHEN** se revisa la telemetría exportada
- **THEN** su recurso tiene el nombre de servicio `sandbox-runner`

### Requirement: Telemetría enviada antes de terminar
Con la exportación activa y un receptor disponible, el Runner SHALL enviar toda la telemetría pendiente antes de que termine el proceso, cualquiera que sea su código de salida.

#### Scenario: Ejecución one-shot con panel
- **GIVEN** el panel de trazas en marcha y el endpoint apuntando a él
- **WHEN** se ejecuta una vez `--scenario happy-order` y el proceso termina
- **THEN** el panel muestra la traza completa de esa ejecución y sus registros

#### Scenario: Parada por un guardrail
- **GIVEN** el panel en marcha, el endpoint apuntando a él y `Guardrails:MaxModelCalls` igual a 1
- **WHEN** se ejecuta `--scenario happy-order` y termina con código 4
- **THEN** el panel muestra la traza de esa ejecución con su motivo de parada

### Requirement: Traza de cada ejecución del agente
Cada ejecución del agente SHALL producir un span raíz `invoke_agent sales-agent` con el identificador de ejecución de la auditoría, el motivo de parada, las llamadas al modelo y los tokens. Todos los spans de la ejecución SHALL pertenecer a esa traza, también si se pausa para pedir una confirmación. Una parada por un guardrail SHALL marcar el span raíz como error.

#### Scenario: Una sola traza con confirmación
- **GIVEN** un modelo que pide `get_order`, después `cancel_order` de un pedido del cliente de sesión, y luego responde, y un aprobador que aprueba
- **WHEN** el agente atiende la petición
- **THEN** los spans de las tres llamadas al modelo, de las dos tools y de la confirmación tienen como padre el span raíz, y todos comparten su identificador de traza

#### Scenario: Atributos del span raíz
- **GIVEN** un modelo que pide `list_products` y luego responde, con 100 tokens de entrada y 10 de salida en cada llamada
- **WHEN** el agente atiende la petición
- **THEN** el span raíz lleva el mismo identificador de ejecución que la auditoría, el motivo `Completed`, 2 llamadas, 200 tokens de entrada y 20 de salida, y no está marcado como error

#### Scenario: Parada por un guardrail
- **GIVEN** `Guardrails:MaxModelCalls` igual a 1 y un modelo que pide `list_products` en cada respuesta
- **WHEN** el agente atiende la petición
- **THEN** el span raíz lleva el motivo `ModelCallLimit` y está marcado como error

### Requirement: Spans de llamadas al modelo
Cada llamada al modelo SHALL producir un span `chat`, hijo del span raíz de la ejecución, con el modelo pedido, el modelo que respondió y los tokens de entrada y de salida, según las convenciones semánticas de OpenTelemetry para IA generativa.

#### Scenario: Dos llamadas
- **GIVEN** un modelo que pide `list_products` y luego responde
- **WHEN** el agente atiende la petición
- **THEN** hay dos spans `chat`, cada uno con el modelo y con los tokens de su llamada

### Requirement: Spans de tools
Cada tool que el agente ejecuta SHALL producir un span `execute_tool`, hijo del span raíz, con el nombre de la tool, el identificador de la petición del modelo y el resultado: éxito o el código de error de la tool. Una tool bloqueada por la allow-list SHALL producir su span con `BLOCKED_BY_POLICY`.

#### Scenario: Tool con error de negocio
- **GIVEN** un modelo que pide `get_order` de `ORD-9999` y luego responde
- **WHEN** el agente atiende la petición
- **THEN** hay un span `execute_tool` de `get_order` cuyo resultado es `ORDER_NOT_FOUND`

#### Scenario: Tool bloqueada
- **GIVEN** la allow-list sin `place_order` y un modelo que pide `place_order` y luego responde
- **WHEN** el agente atiende la petición
- **THEN** hay un span `execute_tool` de `place_order` cuyo resultado es `BLOCKED_BY_POLICY`

### Requirement: Spans de confirmación humana
Cada confirmación que se pide a una persona SHALL producir un span `approval`, hijo del span raíz, con el nombre de la tool y la decisión, y cuya duración es la espera de esa decisión. El span MUST NOT incluir los argumentos de la tool.

#### Scenario: Confirmación aprobada
- **GIVEN** un modelo que pide `cancel_order` de un pedido del cliente de sesión y un aprobador que aprueba
- **WHEN** el agente atiende la petición
- **THEN** hay un span `approval` de `cancel_order` con la decisión `Approved`, y ninguno de sus atributos contiene el identificador del pedido

#### Scenario: Confirmación denegada
- **GIVEN** el mismo modelo y un aprobador que deniega
- **WHEN** el agente atiende la petición
- **THEN** hay un span `approval` de `cancel_order` con la decisión `Denied` y no hay ningún span `execute_tool` de `cancel_order`

### Requirement: Telemetría sin contenido por defecto
Mientras la captura de contenido esté desactivada, que es su valor por defecto, ningún span ni registro exportado MUST contener el texto de la petición, las instrucciones del sistema, los mensajes del modelo ni los argumentos o resultados de las tools.

#### Scenario: Pedido sin contenido en la traza
- **GIVEN** la captura de contenido desactivada y un modelo que pide `place_order` de 2 unidades de `MUG-RED` y luego responde
- **WHEN** el agente atiende "quiero dos tazas rojas"
- **THEN** ningún atributo ni evento de los spans de la ejecución contiene "quiero dos tazas rojas", las instrucciones del sistema, `MUG-RED` ni la clave de idempotencia

### Requirement: Captura de contenido opcional
Con `Telemetry:CaptureContent` activada, los spans `chat` SHALL incluir los mensajes enviados y recibidos, con los argumentos y resultados de las tools, y las definiciones de las tools. Con la exportación activa, el Runner SHALL avisar al arrancar de que la telemetría lleva contenido. El contenido MUST NOT aparecer en los registros de la consola.

#### Scenario: Mensajes en la traza
- **GIVEN** la captura de contenido activada y un modelo que pide `place_order` de 2 unidades de `MUG-RED` y luego responde
- **WHEN** el agente atiende "quiero dos tazas rojas"
- **THEN** los spans `chat` contienen la petición, los argumentos de `place_order` y su resultado

#### Scenario: Aviso al arrancar
- **GIVEN** la captura de contenido activada y la exportación activa
- **WHEN** arranca el Runner
- **THEN** el registro contiene un aviso de que la telemetría incluye el contenido de las conversaciones

#### Scenario: La consola no muestra el contenido
- **GIVEN** la captura de contenido activada
- **WHEN** se ejecuta `--scenario happy-order` con un cliente de chat falso
- **THEN** ninguna línea registrada en la consola contiene el texto de la petición del escenario
