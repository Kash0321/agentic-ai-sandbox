# 3. Function Calling

> **Estado**: documento base. **Verificado**: 2026-10-31 contra Microsoft Learn para la parte de .NET. El formato JSON de abajo es el de las APIs compatibles con OpenAI; **hay que contrastarlo con la documentación de DeepSeek** en la fase 2 (campos y comportamientos concretos pueden variar).

## Qué es

*Function calling* (o *tool calling*) es la capacidad de un modelo de, en lugar de responder con texto, **devolver una petición estructurada para invocar una función** que tú has descrito. Es el mecanismo que convierte un LLM en agente: sin él, el modelo solo habla; con él, puede *actuar* a través de tu código.

Es solo un **contrato de intercambio de mensajes**. La ejecución de la función ocurre siempre en tu aplicación.

## El protocolo, paso a paso

Formato de las APIs compatibles con OpenAI (simplificado).

**1. Petición**: mensajes más la descripción de las tools como JSON Schema.

```json
{
  "model": "deepseek-chat",
  "messages": [
    { "role": "user", "content": "¿En qué estado está el pedido A-17?" }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_order",
        "description": "Obtiene un pedido por su identificador. Úsala para consultar estado e importe.",
        "parameters": {
          "type": "object",
          "properties": {
            "orderId": { "type": "string", "description": "Identificador del pedido, formato A-123" }
          },
          "required": ["orderId"]
        }
      }
    }
  ]
}
```

**2. Respuesta del modelo**: no hay texto final, hay una petición de llamada. Los argumentos llegan como **cadena JSON** que hay que parsear y validar.

```json
{
  "role": "assistant",
  "content": null,
  "tool_calls": [
    {
      "id": "call_001",
      "type": "function",
      "function": { "name": "get_order", "arguments": "{\"orderId\":\"A-17\"}" }
    }
  ]
}
```

**3. Tu aplicación ejecuta la función** y devuelve el resultado como mensaje de rol `tool`, enlazado por `tool_call_id`:

```json
{ "role": "tool", "tool_call_id": "call_001", "content": "{\"success\":true,\"data\":{\"status\":\"Pending\",\"total\":120.50}}" }
```

**4. Se reenvía todo** (historial más resultado) y el modelo responde con otra llamada o con el texto final.

## Detalles importantes

- **Llamadas paralelas**: algunos modelos devuelven varias llamadas en un mismo turno. Tu aplicación las ejecuta y devuelve todos los resultados juntos. `FunctionInvokingChatClient` las soporta, pero por defecto las ejecuta **en serie** (`AllowConcurrentInvocation` es `false`).
- **Control de elección** (`tool_choice` en el estándar OpenAI): normalmente `auto` (el modelo decide), `none` (prohíbe tools) o forzar una concreta. **(No verificado)** el soporte exacto en DeepSeek.
- **El modelo no ve tu código**, solo el nombre, la descripción y el schema de cada parámetro. De ahí que las descripciones sean tan importantes (y que en este proyecto sean obligatorias).
- **Las definiciones consumen tokens**. Microsoft recomienda reducir el número de tools, acortar descripciones o registrar solo las relevantes para la conversación.
- **Los argumentos pueden ser inválidos**: parámetros ausentes, tipos incorrectos o JSON malformado. Microsoft documenta estrategias y la propiedad `IncludeDetailedErrors` (por defecto `false`) para decidir si el detalle de la excepción llega al modelo.
- **Soporte desigual entre modelos**: no todos los modelos ni proveedores soportan function calling, ni llamadas paralelas. Hay que comprobarlo por modelo.

## Cómo se ve en .NET

`AIFunctionFactory` convierte un método en una `AIFunction` con su schema derivado de los tipos y de los `[Description]`; `FunctionInvokingChatClient` ejecuta el bucle de los pasos 2 a 4.

```csharp
[Description("Obtiene un pedido por su identificador. Úsala para consultar estado e importe.")]
static ToolResult<OrderSummary> GetOrder(
    [Description("Identificador del pedido, formato A-123")] string orderId) { /* ... */ }

IChatClient client = new ChatClientBuilder(innerClient)
    .UseFunctionInvocation()
    .Build();

var options = new ChatOptions { Tools = [AIFunctionFactory.Create(GetOrder)] };
var response = await client.GetResponseAsync("¿En qué estado está el pedido A-17?", options);
```

(Ejemplo ilustrativo; las tools reales del proyecto siguen las reglas de `AGENTS.md`, sección 4.)

## Buenas prácticas (y por qué las reglas del proyecto son así)

| Regla del proyecto | Razón |
|--------------------|-------|
| `[Description]` en método y parámetros | Es lo único que el modelo sabe de la tool |
| Parámetros simples y `enum` para conjuntos cerrados | Reducen la probabilidad de argumentos inválidos |
| Pocas tools, finas y semánticas | Menos tokens y menos confusión al elegir |
| Devolver `ToolResult` con error tipado, no lanzar | El modelo entiende un error estructurado; una excepción cruda no ayuda y puede filtrar detalles |
| Validar siempre en la tool | El modelo puede inventar argumentos |
| Salidas compactas | Cada token de resultado se reenvía en cada vuelta |

## Errores típicos

- Confiar en que el modelo respetará el tipo declarado.
- Una descripción vaga ("gestiona pedidos") que lleva al modelo a usar mal la tool.
- Devolver el mensaje de una excepción interna al modelo (puede contener rutas, cadenas de conexión...).
- Olvidar limitar el bucle y encontrarse con decenas de llamadas por petición.

## Referencias

- AI tool calling: <https://learn.microsoft.com/dotnet/ai/conceptual/calling-tools>
- Manejar entradas inválidas del modelo: <https://learn.microsoft.com/dotnet/ai/how-to/handle-invalid-tool-input>
- `FunctionInvokingChatClient.AllowConcurrentInvocation`: <https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient.allowconcurrentinvocation>
- Function calling de OpenAI (formato de referencia) y documentación de DeepSeek: **por contrastar** en la fase 2.
