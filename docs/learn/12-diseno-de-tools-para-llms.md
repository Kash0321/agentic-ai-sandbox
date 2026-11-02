# 12. Diseño de tools para LLMs

> **Estado**: creado con el change `add-sales-agent` (fase 4). Explica cómo se diseña una herramienta (tool) que va a usar un modelo de lenguaje: nombre, descripción, granularidad, parámetros, resultado, errores, el borde que la protege y qué cosas no deben ser parámetros. **Verificado**: 2026-11-02 contra Microsoft Learn (*AI tool calling*, *Handle invalid function input from AI models*, la referencia de `FunctionInvokingChatClient`), la documentación de DeepSeek (*Thinking Mode*), un spike con 18 llamadas reales y 11 llamadas reales más del Runner. Lo que no se ha podido comprobar se marca **(no verificado)**.

## Qué es y por qué importa

Una tool es una función de nuestro código que el modelo puede pedir que se ejecute. El modelo no ve el código: ve un **nombre**, una **descripción** y un **esquema JSON** de parámetros, y después lee el **resultado** como texto. Con eso decide qué hacer. El protocolo está en el [documento 03](03-function-calling.md); este documento trata del diseño.

En la práctica, la calidad de las tools pesa más que la del *prompt*:

- una tool con un nombre ambiguo se invoca cuando no toca;
- una descripción vaga produce argumentos inventados;
- un resultado verboso gasta tokens en cada turno;
- un error sin código deja al modelo sin saber si reintentar.

Y una tool que acepta lo que no debe (un identificador de cliente, por ejemplo) abre la puerta a la *prompt injection*.

## Las tools de este proyecto

| Tool | Parámetros | Qué hace |
|---|---|---|
| `list_products` | (ninguno) | Catálogo con SKU, nombre, precio y disponible |
| `get_stock` | `sku` | Un producto |
| `get_customer` | (ninguno) | El cliente de sesión |
| `get_order` | `orderId` | Un pedido |
| `place_order` | `lines` (`sku`, `quantity`), `idempotencyKey` | Crea un pedido para el cliente de sesión |
| `cancel_order` | `orderId`, `idempotencyKey` | Cancela un pedido |

El contrato completo (resultados y códigos de error) está en la spec `agent-tools`.

## Nombres

- **Estables y explícitos**: `place_order`, no el nombre del método. Por defecto, `AIFunctionFactory` usa el nombre del método **sin `Async`** (`PlaceOrderAsync` da `PlaceOrder`; comprobado en el spike). Si el nombre lo deriva el código, renombrar un método cambia el contrato con el modelo sin que nadie lo note.
- **Verbo y objeto**, en `snake_case`. Las descripciones y el *prompt* los citan, y un test comprueba que solo citan nombres que existen. En el spike, una descripción decía "tal como lo devuelve list_products" cuando la tool se llamaba `ListProducts`; el modelo acertó, pero es un riesgo gratuito.

## Descripciones

La descripción es la documentación que lee el modelo. Buenas prácticas, y por qué:

- **Qué hace y cuándo usarla**: "Úsala para encontrar el SKU de lo que pide el cliente". Sin esa frase, el modelo no siempre sabe que debe consultar el catálogo antes de pedir.
- **Formatos y unidades**: "con el formato ORD-0001", "precio unitario en EUR".
- **Cada parámetro, descrito**. Es una regla de `AGENTS.md`, y un test la comprueba. En un `record` posicional, la descripción tiene que ir en la propiedad (`[property: Description]`) para llegar al esquema (comprobado).
- **Sin reglas de negocio**. Escribir "de 1 a 100 unidades" en la descripción duplica una regla del dominio, y la descripción y el dominio acabarían divergiendo. La regla la aplica el dominio, y su mensaje de error ya dice cómo corregir. Es criterio de diseño propio.

**Coste**: según Microsoft Learn (*AI tool calling*), "Tool descriptions are included in the request sent to the model and count against the model's token limit". Se mandan **en cada llamada**. En la ejecución real de `happy-order`, la primera llamada, que solo lleva instrucciones, petición y esquemas, costó 1.212 tokens de entrada.

## Granularidad: qué tools exponer

- **Una tool por intención del usuario**, no una por tabla. `list_products` existe porque el cliente dice "dos tazas rojas", no `SKU-MUG-RED`: sin ella, el modelo tendría que adivinar el identificador o encontrarlo en el *prompt*.
- **Lecturas y escrituras separadas** (CQRS, [documento 11](11-dominios-aptos-para-agentes.md#cqrs-simplificado)): las de lectura son inofensivas; las de escritura llevan clave de idempotencia y, en la fase 5, guardrails.
- **Solo lo que el rol necesita**: el agente actúa por un cliente, y enviar un pedido es una acción de almacén. Por eso `ShipOrder` existe en la aplicación pero **no** como tool. La herramienta más segura es la que no existe.

## Parámetros y esquema

- **Tipos simples y obligatorios**, sin objetos anidados profundos. `place_order` recibe una lista de objetos (`sku`, `quantity`), que es el máximo razonable.
- **Nada que el modelo no deba decidir**:
  - el `CancellationToken` no aparece en el esquema (`AIFunctionFactory` lo excluye; comprobado);
  - el cliente no es un parámetro: lo fija quien ejecuta el agente (cliente de sesión, [ADR 0004](../adr/0004-identidad-del-cliente-fuera-del-modelo.md)). Si fuera un parámetro, un texto como "pídelo a nombre de CUST-002" bastaría para actuar por otro.
- **El esquema se prueba**: un test por tool comprueba los parámetros y cuáles son obligatorios, y otro, que ningún esquema tiene parámetros de cliente.

## Resultado: compacto y tipado

```
ToolResult<T> = (success, data, error)
error         = (code, message, retryable)
```

- **Siempre la misma forma**, con éxito o con fallo. El modelo aprende a leerla.
- **Vistas compactas**: solo lo que el modelo necesita. Por ejemplo, el stock da el disponible, no existencias y reservas ([documento 11](11-dominios-aptos-para-agentes.md#cqrs-simplificado)).
- **Serialización cuidada**, comprobada en el spike y en el apply:
  - camelCase y estados como texto (`"status":"Placed"`), no números;
  - sin campos nulos: un éxito no lleva `error` y un fallo no lleva `data`;
  - **compacta**: las opciones por defecto de `Microsoft.Extensions.AI` **indentan** el JSON, y cada salto de línea son tokens que se repiten en todos los turnos siguientes (observado en el apply; se corrigió con `WriteIndented = false`);
  - **sin escapar los acentos**: por defecto, "Lámpara" sale como `Lámpara`. En los resultados se corrige con un codificador relajado; en el esquema no, porque su generación no usa ese codificador (observado).

## Errores accionables

Un error para un modelo tiene que decirle **qué pasó, si reintentar y cómo corregir**:

- **Código estable**, del catálogo del dominio (`INSUFFICIENT_STOCK`) o de la capa de tools (`INVALID_ARGUMENTS`, `INTERNAL_ERROR`).
- **`retryable`**: verdadero solo para los errores transitorios (`SERVICE_UNAVAILABLE`). Sale del `ErrorKind` del dominio ([ADR 0003](../adr/0003-errores-de-dominio-como-resultados.md)). Con `INSUFFICIENT_STOCK`, reintentar lo mismo no sirve de nada.
- **Mensaje con el dato que permite corregir**: "No hay stock suficiente de SKU-TSHIRT-M: quedan 5 unidades disponibles."

## El borde: nada de excepciones hacia el modelo

`AGENTS.md` prohíbe que una tool lance excepciones hacia el modelo. Por defecto, si algo falla, `FunctionInvokingChatClient` le envía **"Error: Function failed."**, sin código (observado). Microsoft Learn (*Handle invalid function input from AI models*) confirma que, por defecto, "the AI model receives a generic error message". También explica que `IncludeDetailedErrors` le pasaría el mensaje de la excepción, con la advertencia de que "can expose internal system details". Aquí se resuelve con un borde en dos niveles:

```
FunctionInvokingChatClient
  FunctionInvoker (borde exterior, uno para todas)
    | registra nombre, duración y resultado (sin argumentos)
    | ArgumentException al asignar argumentos --> INVALID_ARGUMENTS
    v
  método de la tool (borde interior)
    | resultado de la aplicación --> ToolResult
    | excepción inesperada --> INTERNAL_ERROR (detalle solo en el registro)
```

- **Argumentos que no cumplen el esquema**: si falta un parámetro obligatorio, el `ArgumentException` salta al asignar los argumentos, **antes** de ejecutar la tool. El mensaje interno está en inglés ("The arguments dictionary is missing a value for the required parameter 'sku'") y su `ParamName` es `arguments`. No se reenvía: el modelo recibe `INVALID_ARGUMENTS` con un mensaje que nombra la tool (comprobado con un test de aprendizaje).
- **La cancelación tiene un matiz** (comprobado): `FunctionInvokingChatClient` propaga una `OperationCanceledException` solo si **el token de la petición está cancelado**. Si una tool la lanza sin cancelación real, el bucle la trata como un fallo más y continúa. Por eso el borde interior no la convierte en `INTERNAL_ERROR`: la deja pasar y decide el bucle.

## Idempotencia en manos del modelo

Los commands (`place_order`, `cancel_order`) reciben una clave de idempotencia que **genera el modelo**. La descripción le pide "una nueva para cada petición distinta y exactamente la misma si repites tras un error reintentable".

| Alternativa | Problema |
|---|---|
| La clave sale del identificador de la llamada (`CallId`) | Cada reintento del modelo es una llamada nueva: no protege de la respuesta perdida |
| La clave se deriva de los argumentos | Impide pedir dos veces lo mismo a propósito, y oculta el concepto |
| **La pone el modelo** (elegida) | Depende de que el modelo la reutilice |

**Lo observado** con `deepseek-flash`, provocando una respuesta perdida (el inyector ejecuta `PlaceOrder` y devuelve `SERVICE_UNAVAILABLE`):

- spike: 2 de 2 reintentos con la misma clave (`tazas-rojas-2-001`, `mugs-rojas-ana-001`);
- Runner real (`--scenario lost-response`): 2 de 2. Las respuestas citan `ORD-0001`; con otra clave, el reintento habría creado `ORD-0002`;
- las claves son semánticas, a veces con fechas antiguas (`mug-red-2-20250214-01`), y siempre válidas.

Son 4 de 4, una muestra pequeña: **no es una garantía**. Lo que la garantiza es el dominio (misma clave, mismo resultado). El modelo solo tiene que acertar con la clave, y la fase 6 lo medirá con más ejecuciones.

## Cómo se aplica en este proyecto

| Pieza | Dónde |
|---|---|
| Contrato (`ToolResult`, `ToolError`, códigos, serialización) | [`src/Sandbox.Agents/Tools/`](../../src/Sandbox.Agents/Tools/) |
| Las seis tools y el cliente de sesión | [`SalesTools.cs`](../../src/Sandbox.Agents/Tools/SalesTools.cs), [`SalesSession.cs`](../../src/Sandbox.Agents/Tools/SalesSession.cs) |
| Agente, borde exterior y system prompt | [`src/Sandbox.Agents/Agent/`](../../src/Sandbox.Agents/Agent/) |
| Escenarios y opciones del Runner | [`src/Sandbox.Runner/Agent/`](../../src/Sandbox.Runner/Agent/) |
| Tests de esquema, de cada código de error y del agente | [`tests/Sandbox.Agents.Tests/`](../../tests/Sandbox.Agents.Tests/) |
| Tests de aprendizaje de `FunctionInvokingChatClient` | [`tests/Sandbox.Agents.Tests/Learning/`](../../tests/Sandbox.Agents.Tests/Learning/) |
| Requisitos | Specs `agent-tools`, `sales-agent` y `runner-host` |
| Decisiones y experimentos | `design.md` del change `add-sales-agent`; [ADR 0004](../adr/0004-identidad-del-cliente-fuera-del-modelo.md) |

Para probarlo:

```bash
dotnet run --project src/Sandbox.Runner -- --scenario happy-order
dotnet run --project src/Sandbox.Runner -- --scenario lost-response
dotnet run --project src/Sandbox.Runner -- --prompt "¿Qué productos tenéis?" --customer CUST-002
```

## Errores típicos

- **Dejar que el nombre de la tool lo derive el código** (`PlaceOrder` en vez de `place_order`): cambia sin aviso al renombrar.
- **Descripciones que repiten reglas del dominio**: acaban divergiendo de lo que hace el dominio.
- **Parámetros que el modelo no debe controlar**: el cliente, el precio o el estado.
- **Resultados con la entidad completa** o JSON indentado: tokens en cada turno.
- **Errores sin código o sin `retryable`**: el modelo reintenta lo que no tiene arreglo, o se rinde ante un fallo pasajero.
- **Activar `IncludeDetailedErrors`** para "ayudar al modelo": le envía mensajes internos, en inglés y quizá con datos sensibles.
- **Confiar en que el modelo envía argumentos válidos**: le faltan parámetros, cambia tipos o envía `null`. El borde lo traduce a `INVALID_ARGUMENTS`, y el dominio valida el resto.
- **Exponer tools "por si acaso"** (`ship_order` a un agente de cliente): cada tool es superficie de ataque.

## Referencias

- `AGENTS.md`, sección 4 (reglas estrictas para tools).
- Microsoft Learn, *AI tool calling* (bucle, llamadas en paralelo, coste en tokens de las descripciones): https://learn.microsoft.com/dotnet/ai/conceptual/calling-tools
- Microsoft Learn, *Handle invalid function input from AI models* (error genérico por defecto, `IncludeDetailedErrors`): https://learn.microsoft.com/dotnet/ai/how-to/handle-invalid-tool-input
- Microsoft Learn, referencia de `FunctionInvokingChatClient` (bucle, `MaximumIterationsPerRequest`, `ApprovalRequiredAIFunction`): https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient
- Microsoft Learn, referencia de `AIFunctionFactory`: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunctionfactory
- DeepSeek, *Thinking Mode* (razonamiento con tools): https://api-docs.deepseek.com/guides/thinking_mode
- Documentos [03](03-function-calling.md) (protocolo), [05](05-guardrails.md) (guardrails), [06](06-autenticacion-y-autorizacion.md) (identidad) y [11](11-dominios-aptos-para-agentes.md) (dominio).
