# ADR 0005: Autorización por recurso en la aplicación, sin revelar lo ajeno

- **Estado**: aceptada
- **Fecha**: 2026-11-02
- **Change**: `add-agent-guardrails` (decisiones D1, D2 y D3 de su `design.md`)

## Contexto

El ADR 0004 sacó la identidad del cliente del contrato de las tools: el agente actúa siempre en nombre de un **cliente de sesión** que fija quien lo ejecuta. Dejó una limitación conocida: `get_order` y `cancel_order` reciben un identificador de pedido y no comprobaban que fuera del cliente de sesión. Con números de pedido secuenciales (`ORD-0001`, `ORD-0002`…), cualquier cliente podía leer o **cancelar** el pedido de otro. El modelo no necesita "querer" atacar: basta un número inventado, una conversación confusa o un texto inyectado.

Es el fallo que OWASP sitúa en primer lugar de su lista de riesgos de APIs: **Broken Object Level Authorization** (API1:2023), cambiar el identificador de un objeto para llegar a datos de otro usuario.

Al estudiar la corrección en el explore apareció una segunda vía. Las claves de idempotencia eran **globales**, y la repetición de un command se comprueba **antes** de ejecutarlo. Si un cliente cancelaba su pedido con la clave `k1` y otro repetía la misma petición con `k1`, recibía la vista guardada del pedido ajeno. Eso ocurría aunque se añadiera la comprobación de pertenencia, porque esa comprobación nunca llegaba a ejecutarse.

## Decisión

1. **La pertenencia la decide la aplicación**, no el agente ni el prompt. `GetOrder` y `CancelOrder` reciben el cliente que pide la operación, y la aplicación lo compara con el propietario del pedido. En `CancelOrder` la comprobación y la transición ocurren en la misma sección crítica. `ShipOrder` es una operación del almacén y no lleva cliente.
2. **Un pedido ajeno es indistinguible de uno inexistente**: `ORDER_NOT_FOUND`, con el mismo mensaje. Así no se confirma a un tercero que el pedido existe. El motivo real queda en un evento de auditoría (`AccessDenied`).
3. **Las claves de idempotencia se guardan por ámbito**: el cliente en `PlaceOrder` y `CancelOrder`, y el almacén en `ShipOrder`. La misma clave en dos ámbitos son dos claves independientes. Por eso la repetición de un cliente nunca encuentra el resultado guardado de otro, y la comprobación de pertenencia se ejecuta siempre.

## Alternativas consideradas

- **Comprobar en la tool** (leer el pedido y comparar antes de actuar): mete lógica en una tool que debe ser fina, abre una carrera entre la lectura y la cancelación, y deja sin proteger a cualquier otro llamador de la aplicación.
- **Comprobar en un guardrail del bucle**: el guardrail tendría que consultar el dominio, lo que acopla la capa de invocación del LLM a las reglas de negocio (`AGENTS.md`, principio 1).
- **Código propio `FORBIDDEN`**, como preveía el documento 06: es más explícito al depurar, pero el modelo puede contar al usuario que ese pedido existe y es de otro cliente. GitHub sigue el mismo criterio en su API: responde `404 Not Found` en lugar de `403 Forbidden` "para no confirmar la existencia de repositorios privados".
- **Meter el cliente en la huella de la clave**, con la clave global: el otro cliente recibiría `IDEMPOTENCY_KEY_CONFLICT`, que le confirma que esa clave la usó alguien.
- **Identificadores de pedido aleatorios** (OWASP los recomienda como medida adicional): reducen la posibilidad de adivinar un número, pero no sustituyen a la comprobación. Se dejan fuera para no cambiar el dominio de la fase 3; con la comprobación en la aplicación, adivinar un número ya no da acceso a nada.

## Consecuencias

- **Un cliente no puede leer ni cancelar pedidos de otro**, lo intente quien lo intente: el modelo, un texto inyectado o un llamador directo de la aplicación.
- **Lo que ve el modelo no distingue** entre "no existe" y "no es tuyo". El diagnóstico se hace con la auditoría, no con la respuesta.
- **Orden de las comprobaciones**: cualquier atajo que devuelva un resultado guardado (idempotencia, cachés) tiene que estar dentro del ámbito del que pide, o se salta la autorización. Es la lección general de este ADR.
- **Efecto colateral aceptado**: la misma clave en `PlaceOrder` y luego en `ShipOrder` ya no da conflicto, porque están en ámbitos distintos. La spec solo exigía el conflicto entre commands del mismo cliente.
- **Cambio de contrato interno**: `GetOrderQuery` y `CancelOrderCommand` llevan el cliente. Solo los construyen las tools y los tests.
- **En un sistema real** el cliente saldría de una identidad autenticada (ADR 0004), y la auditoría iría a un almacén duradero e inalterable, no a un registro.

## Referencias

- OWASP API Security Top 10 2023, [API1:2023 Broken Object Level Authorization](https://api-security.owasp.org/editions/2023/en/0xa1-broken-object-level-authorization) (consultado el 2026-11-02).
- GitHub REST API, [Troubleshooting the REST API](https://docs.github.com/en/rest/using-the-rest-api/troubleshooting-the-rest-api), apartado sobre `404 Not Found` (consultado el 2026-11-02).
- [ADR 0004](0004-identidad-del-cliente-fuera-del-modelo.md): la identidad del cliente no la decide el modelo.
- Specs `order-operations` ("Commands CancelOrder y ShipOrder", "Queries de solo lectura", "Ámbito de las claves de idempotencia", "Auditoría de los accesos denegados") y `agent-tools` ("Tool get_order", "Tool cancel_order").
- Documentos de aprendizaje: [05, guardrails](../learn/05-guardrails.md) y [06, autenticación y autorización](../learn/06-autenticacion-y-autorizacion.md).
