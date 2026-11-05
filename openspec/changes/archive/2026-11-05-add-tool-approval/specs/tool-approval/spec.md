## Purpose

Define la confirmación humana de las acciones irreversibles del agente: qué tools la requieren, cómo se pide con la acción y los argumentos exactos, qué ocurre al aprobar y al denegar, cómo encaja con la allow-list y quién decide, en consola o con un guion reproducible. La decisión la toma una persona, nunca el modelo.

## ADDED Requirements

### Requirement: Acciones que requieren confirmación
`cancel_order` SHALL requerir una confirmación humana antes de ejecutarse; el resto de tools no la requieren. Qué tools la requieren MUST fijarse en código y no ser configurable. Mientras no haya decisión, la acción MUST NOT ejecutarse.

#### Scenario: Cancelación pendiente de confirmar
- **GIVEN** el pedido `ORD-0001` en `Placed` del cliente de sesión y un modelo que pide `cancel_order` de `ORD-0001`
- **WHEN** el agente atiende la petición y se pide la decisión al aprobador
- **THEN** en ese momento `ORD-0001` sigue en `Placed`

#### Scenario: Tool sin confirmación
- **GIVEN** un modelo que pide `list_products` y después responde con texto
- **WHEN** el agente atiende la petición
- **THEN** no se pregunta nada al aprobador

### Requirement: Aprobación ligada a la acción exacta
El aprobador SHALL recibir el nombre de la tool y los argumentos exactos con que se ejecutará. Si aprueba, SHALL ejecutarse exactamente esa petición, con esos argumentos, y la ejecución SHALL seguir con normalidad.

#### Scenario: Cancelación aprobada
- **GIVEN** `ORD-0001` en `Placed` del cliente de sesión, un aprobador que aprueba y un modelo que pide `cancel_order` de `ORD-0001` y después responde con texto
- **WHEN** el agente atiende la petición
- **THEN** el aprobador recibió `cancel_order` con `orderId` igual a `ORD-0001`, el pedido queda en `Cancelled`, el resultado lista `cancel_order` con éxito y el motivo de parada es `Completed`

### Requirement: Confirmación denegada
Si el aprobador deniega, la tool MUST NOT ejecutarse. El modelo SHALL recibir un aviso de que la acción no se confirmó y no debe repetirse; el resultado SHALL listar la tool con `CONFIRMATION_DENIED`, y la denegación SHALL contar como error seguido (`agent-guardrails`). Si nada la para, la ejecución SHALL terminar con `Completed`.

#### Scenario: Cancelación denegada
- **GIVEN** `ORD-0001` en `Placed` del cliente de sesión, un aprobador que deniega y un modelo que pide `cancel_order` de `ORD-0001` y después responde con texto
- **WHEN** el agente atiende la petición
- **THEN** `ORD-0001` sigue en `Placed`, el modelo recibe el aviso de que no se confirmó, el resultado lista `cancel_order` con `CONFIRMATION_DENIED` y el motivo de parada es `Completed`

### Requirement: Peticiones sin confirmación en la misma respuesta
Si una respuesta del modelo pide a la vez tools que requieren confirmación y tools que no, solo las primeras SHALL llegar al aprobador; las demás SHALL ejecutarse sin preguntar.

#### Scenario: Consulta y cancelación juntas
- **GIVEN** `ORD-0001` en `Placed` del cliente de sesión y un modelo que en una sola respuesta pide `get_order` y `cancel_order` de `ORD-0001`
- **WHEN** el agente atiende la petición con un aprobador que aprueba
- **THEN** el aprobador recibe una sola petición, la de `cancel_order`, y las dos tools se ejecutan

### Requirement: Confirmación y allow-list
Si una tool que requiere confirmación no está permitida por la allow-list, el aprobador MUST NOT recibir ninguna petición, y la tool SHALL tratarse como cualquier tool no permitida (`BLOCKED_BY_POLICY`).

#### Scenario: Cancelación sin permiso
- **GIVEN** `AllowedTools` sin `cancel_order` y un modelo que pide `cancel_order` de `ORD-0001`
- **WHEN** el agente atiende la petición
- **THEN** no se pregunta nada al aprobador, el modelo recibe `BLOCKED_BY_POLICY` y el pedido no cambia

### Requirement: Aprobador de consola
En el Runner, el aprobador SHALL escribir por la salida de errores la tool y sus argumentos en JSON y preguntar `[s/N]`. Solo `s`, `si` o `sí`, sin distinguir mayúsculas y sin espacios alrededor, SHALL aprobar; cualquier otra respuesta, incluida una vacía, SHALL denegar. Los argumentos MUST mostrarse en JSON para que un carácter de control enviado por el modelo no llegue a la terminal.

#### Scenario: Aprobación con "s"
- **GIVEN** una consola interactiva en la que se responde `s`
- **WHEN** el aprobador recibe `cancel_order` con `orderId` igual a `ORD-0001`
- **THEN** la salida de errores muestra `cancel_order` y `"orderId":"ORD-0001"`, y la decisión es aprobar

#### Scenario: Aprobación con "sí"
- **GIVEN** una consola interactiva en la que se responde `Sí`
- **WHEN** el aprobador recibe una petición
- **THEN** la decisión es aprobar

#### Scenario: Respuesta vacía
- **GIVEN** una consola interactiva en la que se pulsa Intro sin escribir nada
- **WHEN** el aprobador recibe una petición
- **THEN** la decisión es denegar

#### Scenario: Carácter de control en un argumento
- **GIVEN** un argumento que contiene el carácter de escape `ESC` (U+001B)
- **WHEN** el aprobador lo muestra
- **THEN** la salida de errores contiene su forma escapada en JSON y no el carácter

### Requirement: Sin consola interactiva
Si la entrada estándar no es una consola interactiva (está redirigida o no existe), el aprobador de consola MUST denegar sin esperar respuesta, con el motivo "sin consola interactiva".

#### Scenario: Entrada redirigida
- **GIVEN** el Runner con la entrada estándar redirigida
- **WHEN** el aprobador recibe una petición
- **THEN** deniega sin leer la entrada y el motivo es "sin consola interactiva"

### Requirement: Auditoría de cada decisión
Cada decisión de aprobación SHALL dejar un evento de auditoría (`agent-guardrails`, "Auditoría estructurada") con la tool, la decisión (`Approved` o `Denied`), el aprobador que decidió y, si lo hay, el motivo. El evento MUST NOT incluir los argumentos de la tool.

#### Scenario: Denegación auditada
- **GIVEN** un aprobador con guion que deniega `cancel_order`
- **WHEN** termina la ejecución
- **THEN** la auditoría contiene un evento de decisión con `cancel_order`, `Denied` y el aprobador, sin el identificador del pedido ni la clave de idempotencia
