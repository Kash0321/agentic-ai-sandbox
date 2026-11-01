# 4. Herramientas de Microsoft: qué problema resuelven y cómo

> **Estado**: documento base. **Verificado**: 2026-10-31 contra Microsoft Learn. Versiones estables más recientes en NuGet a esa fecha: `Microsoft.Extensions.AI` (y `.Abstractions` y `.OpenAI`) 10.11.0, `OpenAI` 2.14.0, `Microsoft.Extensions.Hosting` 10.0.14, `Microsoft.Agents.AI` 1.27.0, `ModelContextProtocol` 2.3.0. **Corrección**: la primera versión de este documento citaba 10.10.x y 1.13.0, copiadas de páginas de referencia de Microsoft Learn que estaban desfasadas. Las versiones **no se leen de la documentación**: se comprueban en la fuente primaria (NuGet) al fijarlas, y se reverifican en cada fase (ver `AGENTS.md`, sección 11). **Fase 2** (change `add-deepseek-chat-client`, 2026-11-01): reverificadas las mismas versiones en NuGet, todas con licencia MIT. La sección 4 recoge lo comprobado al conectar DeepSeek con llamadas reales y con tests sin red.

## El problema de fondo

Cada proveedor de LLM tiene su propio SDK, sus tipos y su manera de hacer chat, streaming y tool calling. Si tu código depende directamente de uno, cambiar de modelo, probar con un doble de pruebas o añadir transversales (logging, caché, telemetría) es caro. Las librerías de Microsoft atacan este problema por capas:

```
 Tu aplicación (Runner, tools, guardrails)
        |
        v
 Agent Framework (opcional)   agentes, sesiones, workflows, aprobaciones
        |
        v
 Microsoft.Extensions.AI      pipeline de IChatClient: invocación de tools,
        |                      telemetría, caché, middleware propio
        v
 Abstractions                 IChatClient, IEmbeddingGenerator, AIFunction
        |
        v
 Adaptador de proveedor       Microsoft.Extensions.AI.OpenAI (endpoint compatible)
        |
        v
 API del modelo               DeepSeek
```

## 1. `Microsoft.Extensions.AI.Abstractions`

**Qué resuelve**: un contrato común para hablar con modelos de IA.

**Cómo**: define los tipos de intercambio. Los principales:

- **`IChatClient`**: abstracción de un cliente de chat (mensajes, multimodal, respuesta completa o en *streaming*).
- **`IEmbeddingGenerator<TInput,TEmbedding>`**: generación de *embeddings*.
- **`ChatMessage`, `ChatOptions`, `ChatResponse`**: mensajes, opciones (incluye la lista de tools) y respuesta.
- **`AIFunction` / `AITool`**: una función que se puede describir al modelo e invocar.

Cualquier librería que ofrezca un cliente LLM puede implementar `IChatClient`. Esto permite **cambiar de proveedor sin tocar la aplicación** y **sustituirlo por un doble en tests**, que es justo lo que exige este proyecto.

> Aviso oficial en la documentación de `IChatClient`: las aplicaciones deben considerar riesgos como *prompt injection*, tamaño de datos y número de mensajes. Las implementaciones **no tienen por qué** traer salvaguardas. Es responsabilidad de la aplicación (de nuestros guardrails).

## 2. `Microsoft.Extensions.AI`

**Qué resuelve**: las funcionalidades transversales alrededor del cliente, sin acoplar la aplicación.

**Cómo**: un **pipeline de middleware** al estilo ASP.NET Core. `ChatClientBuilder` encadena clientes; cada capa envuelve a la siguiente. La clase base `DelegatingChatClient` permite escribir tu propia capa (la documentación incluye un ejemplo de limitación de tasa) y se recomienda ofrecer un método `Use*` para registrarla.

Capas incluidas o disponibles:

| Capa | Método | Para qué |
|------|--------|----------|
| Invocación de funciones | `UseFunctionInvocation()` | Ejecuta automáticamente el bucle de tool calling |
| Telemetría | `UseOpenTelemetry()` | Trazas y métricas estándar |
| Caché | caché distribuida | Evitar llamadas repetidas |
| Reducción de chat (experimental) | `MessageCountingChatReducer`, `SummarizingChatReducer` | Controlar el tamaño del historial |
| Enrutado y *failover* (experimental) | `RoutingChatClient`, `FailoverChatClient` | Elegir modelo o conmutar si falla |
| **Las tuyas** | `DelegatingChatClient` | **Guardrails** (ver documento 5) |

**Por qué es la pieza central del proyecto**: los guardrails de entrada, de bucle y de auditoría se implementan como capas de este pipeline.

## 3. Tool calling en M.E.AI: `AIFunction`, `AIFunctionFactory`, `FunctionInvokingChatClient`

**Qué resuelve**: no escribir a mano el bucle de function calling ni el schema JSON de cada tool.

**Cómo**:

- **`AIFunctionFactory.Create(método)`** genera una `AIFunction` a partir de un método .NET. El nombre, la descripción (`[Description]`) y el schema salen de la firma. **Observado en el explore de la fase 2**: con una función local de las instrucciones de nivel superior, el nombre generado fue `_Main_g_GetOrderStatus_0_2`, que es el nombre interno que le da el compilador, y así se lo envió al modelo. Hay que dar **siempre un nombre explícito** (`AIFunctionFactory.Create(método, name: "get_order_status")`), como pide `AGENTS.md` (sección 4).
- **`FunctionInvokingChatClient`** envuelve un `IChatClient`: cuando el modelo pide una tool, la invoca, recoge el resultado y continúa la conversación hasta que hay respuesta final.

Propiedades relevantes para guardrails (valores por defecto documentados):

| Propiedad | Por defecto | Uso en el proyecto |
|-----------|-------------|--------------------|
| `MaximumIterationsPerRequest` | 40 | Tope del bucle: se fijará mucho más bajo |
| `MaximumConsecutiveErrorsPerRequest` | 3 | Corta tras fallos seguidos de tools |
| `IncludeDetailedErrors` | `false` | Se mantiene en `false`: no filtrar detalle interno al modelo |
| `AllowConcurrentInvocation` | `false` | Ejecución en serie, más predecible |

`FunctionInvocationContext` expone la función, los argumentos y el número de iteración, útil para filtros de autorización y auditoría **(a confirmar en la fase 5 cómo enganchar un filtro por invocación)**.

## 4. `Microsoft.Extensions.AI.OpenAI`

**Qué resuelve**: conectar el SDK oficial de OpenAI (`OpenAI`) con `IChatClient`.

**Cómo**: método de extensión `AsIChatClient()` sobre un `ChatClient` de OpenAI. Como DeepSeek expone una API compatible con OpenAI, se usa este mismo adaptador apuntando el cliente a otro *endpoint* (la clave se configura en el cliente OpenAI).

### 4.1. Conectar DeepSeek (comprobado en la fase 2)

Lo que sigue se comprobó el 2026-11-01 con `OpenAI` 2.14.0 y `Microsoft.Extensions.AI.OpenAI` 10.11.0: primero con una consola desechable en el explore y después con el Runner y con tests que usan un transporte HTTP simulado. El código está en [`src/Sandbox.Agents/Chat/`](../../src/Sandbox.Agents/Chat/).

**El endpoint** se fija en `OpenAIClientOptions.Endpoint`, y la clave con un `ApiKeyCredential`:

```csharp
var options = new OpenAIClientOptions { Endpoint = new Uri("https://api.deepseek.com") };
IChatClient client = new ChatClient("deepseek-flash", new ApiKeyCredential(apiKey), options).AsIChatClient();
```

El SDK hace `POST https://api.deepseek.com/chat/completions` con la cabecera `Authorization: Bearer <clave>`. Lo comprueba un test (`DeepSeekChatClientFactoryTests`) que captura la petición sin red.

**Los modelos de DeepSeek** (documentación de DeepSeek, *Models & Pricing*, consultada el 2026-11-01) son `deepseek-flash` y `deepseek-v4-pro`. Los nombres antiguos, `deepseek-chat` y `deepseek-reasoner`, se siguen aceptando, pero **la respuesta dice que contestó `deepseek-flash`**: es un remapeo silencioso. Lo vimos en el explore y otra vez con el Runner (tarea 4.2): con `DeepSeek__Model=deepseek-chat`, el registro avisa "se pidió deepseek-chat y respondió deepseek-flash". Por eso `UsageLoggingChatClient` compara el modelo pedido con el que respondió.

**El modo *thinking*.** Los dos modelos razonan antes de contestar **por defecto**. La respuesta trae el razonamiento en el campo `reasoning_content`, y el adaptador lo convierte en un `TextReasoningContent`, separado del `TextContent` de la respuesta. Según Microsoft Learn, `TextReasoningContent` representa el "pensamiento" del modelo y es distinto del texto de salida: ninguno de los dos tipos deriva del otro. Por tanto, `response.Text` solo devuelve la respuesta. Medido con "Responde solo con la palabra: hola":

| Modo | Tokens | De ellos, razonamiento | Latencia |
|---|---|---|---|
| *Thinking* activado (por defecto de DeepSeek) | 53 | 11 | 1,3 s |
| *Thinking* desactivado | 15 | 0 | 1,3 s |

**Desactivar el razonamiento** exige enviar `"thinking": {"type": "disabled"}`, un campo que no existe en la API de OpenAI y que `ChatOptions` no puede expresar. Se hace en dos pasos:

1. `ChatOptions.RawRepresentationFactory` permite devolver las opciones **propias del proveedor** (aquí, `ChatCompletionOptions` del SDK de OpenAI). Microsoft Learn lo describe como la vía para "un ajuste específico de la implementación que no exponen las propiedades tipadas de `ChatOptions`", y recomienda devolver una instancia nueva en cada llamada.
2. `ChatCompletionOptions.Patch` es un `JsonPatch` de `System.ClientModel` que añade campos arbitrarios al JSON de la petición: `Patch.Set("$.thinking"u8, BinaryData.FromString("""{"type":"disabled"}"""))`. **Es una API experimental**: el compilador emite el diagnóstico `SCME0001`, que en este proyecto es un error (`TreatWarningsAsErrors`). Se suprime solo en [`ThinkingSwitch.cs`](../../src/Sandbox.Agents/Chat/ThinkingSwitch.cs), con un comentario que lo justifica, y un test sin red comprueba el JSON enviado: si una versión del SDK lo rompe, falla ese test.

El Runner deja el razonamiento desactivado por defecto (`DeepSeek:Thinking=false`), porque el saludo no lo necesita y cuesta tokens. El modelo y el razonamiento se configuran en `appsettings.json`, en variables de entorno o en la línea de comandos ([documento 10](10-configuracion-y-secretos-en-net.md)).

**Caché de *prompt***: DeepSeek la aplica sola, sin pedirla. En la segunda petición de una *tool call*, 256 de los 399 tokens de entrada salieron de caché (campos `prompt_cache_hit_tokens` y `prompt_cache_miss_tokens` de `usage`, que el adaptador no expone como propiedades tipadas). Importa en la fase 4: un bucle de tools reenvía el mismo prefijo una y otra vez.

**Errores: el 401 repite parte de la clave.** Con una clave inválida, DeepSeek responde HTTP 401 con el mensaje "Authentication Fails, Your api key: \*\*\*\*xxxx is invalid", donde `xxxx` son **los 4 últimos caracteres de la clave enviada**. El SDK lo convierte en un `ClientResultException` cuyo `Message` incluye ese texto, así que registrarlo o mostrarlo filtraría parte de la clave. `ProviderErrorTranslatingChatClient` lo sustituye por un `ChatProviderException` con solo el tipo de error y el código HTTP, sin el mensaje ni la excepción original. Lo comprobamos de verdad (tarea 4.2): con una clave inválida, el Runner sale con el código 3 y ni su salida ni sus registros contienen fragmentos de la clave.

**Reintentos implícitos del SDK.** Medido con un transporte simulado: por defecto, el SDK de OpenAI **reintenta 3 veces** (4 llamadas) ante un 429 o un fallo de conexión. Tras agotar los reintentos de un fallo de conexión, lanza un `AggregateException` en vez de un `ClientResultException`. El proyecto fija `RetryPolicy = new ClientRetryPolicy(maxRetries: 0)` por tres motivos: cada reintento de un LLM cuesta tokens, no queda registrado, y el `AggregateException` escaparía a la traducción de errores. Sin reintentos, el fallo de conexión llega como `ClientResultException` con `Status = 0`. La política de reintentos explícita es de la fase 5.

**Medido con el Runner** (tareas 4.1 y 4.2, `deepseek-flash` sin razonamiento): el saludo tarda 1,1–1,4 s de llamada (unos 5 s el proceso entero con `dotnet run`) y gasta 13 tokens de entrada y 2 de salida.

**Qué no se ha comprobado todavía**: si con el razonamiento activado y tools hay que reenviar `reasoning_content` en los turnos siguientes (en el explore llegó vacío en los turnos con tools), el *streaming* contra DeepSeek y los límites de peticiones. Son de la fase 4.

## 5. Microsoft Agent Framework (referencia, **fuera del alcance inicial**)

**Qué resuelve**: construir agentes y sistemas multi-agente de nivel superior. Es el **sucesor directo de Semantic Kernel y AutoGen**, creado por los mismos equipos: combina las abstracciones simples de AutoGen con las funciones empresariales de Semantic Kernel (estado por sesión, tipado, filtros, telemetría) y añade **workflows** basados en grafos.

**Cómo**: un agente (`AIAgent`, normalmente `ChatClientAgent`) se construye sobre cualquier `IChatClient` (`client.AsAIAgent(...)`), con sesiones, herramientas y *middleware*. Ofrece orquestaciones: **secuencial, concurrente, *handoff*, chat de grupo y Magentic**, y aprobación humana de herramientas (`ApprovalRequiredAIFunction`).

**Decisión para el sandbox (criterio propio, a confirmar con un ADR)**: no se usa. El objetivo es entender **los primitivos** (`IChatClient`, tools, pipeline, guardrails) antes de adoptar un framework que los oculta. Es un buen candidato para una comparación posterior: reimplementar un escenario con Agent Framework y contrastar.

## 6. Semantic Kernel (referencia)

Framework previo de orquestación de Microsoft. Existe una guía oficial de migración a Agent Framework. **No se usa** en el proyecto; se menciona porque se encontrará en código y artículos antiguos (`Kernel`, `[KernelFunction]`, plugins).

## 7. MCP C# SDK (`ModelContextProtocol`)

**Qué resuelve**: exponer o consumir herramientas y datos mediante el **Model Context Protocol**, un estándar abierto para integrar apps de IA con herramientas y fuentes de datos. El SDK oficial de C# se mantiene entre Microsoft, Anthropic y la organización MCP, y se apoya en `Microsoft.Extensions.AI`. **Fuera del alcance inicial**; ver backlog.

## 8. Identidad y seguridad (resumen; detalle en el documento 6)

**Microsoft Entra Agent ID**, el flujo OAuth 2.0 *On-Behalf-Of*, las identidades administradas y la PIM resuelven cómo se autentica y autoriza un agente. En el sandbox **se simulan** (no se usa Entra real), pero el diseño imita sus conceptos.

## Referencia rápida

| Concepto | Tipo / paquete | Resuelve | En el proyecto | Documentación |
|----------|----------------|----------|----------------|---------------|
| Cliente de chat | `IChatClient` / Abstractions | Contrato único con el modelo | Sí | [IChatClient](https://learn.microsoft.com/dotnet/ai/ichatclient) |
| Pipeline | `ChatClientBuilder` / M.E.AI | Componer capas | Sí | [Pipelines](https://learn.microsoft.com/dotnet/ai/ichatclient#functionality-pipelines) |
| Middleware propio | `DelegatingChatClient` / M.E.AI | Guardrails y transversales | Sí | [Middleware](https://learn.microsoft.com/dotnet/ai/ichatclient#custom-ichatclient-middleware) |
| Tool | `AIFunction`, `AIFunctionFactory` | Describir e invocar funciones | Sí | [Tool calling](https://learn.microsoft.com/dotnet/ai/conceptual/calling-tools) |
| Bucle de tools | `FunctionInvokingChatClient` | Ejecutar el ciclo automáticamente | Sí | [Tool calling](https://learn.microsoft.com/dotnet/ai/conceptual/calling-tools) |
| Entrada inválida | `IncludeDetailedErrors` | Qué ve el modelo al fallar una tool | Sí | [Entrada inválida](https://learn.microsoft.com/dotnet/ai/how-to/handle-invalid-tool-input) |
| Telemetría | `UseOpenTelemetry()` | Trazas y métricas | Sí (fase 2 o 5) | [M.E.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) |
| Adaptador OpenAI | `Microsoft.Extensions.AI.OpenAI` | DeepSeek vía endpoint compatible | Sí (fase 2) | [AsIChatClient](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.openaiclientextensions.asichatclient) |
| Reductores de chat | `MessageCountingChatReducer`... | Limitar historial | Opcional | [IChatClient](https://learn.microsoft.com/dotnet/ai/ichatclient#chat-reduction-experimental) |
| Agentes y workflows | Microsoft Agent Framework | Orquestación de alto nivel | No (comparación futura) | [Overview](https://learn.microsoft.com/agent-framework/overview/) |
| Aprobación humana | `ApprovalRequiredAIFunction` | Confirmar tools sensibles | Se emulará con un guardrail propio | [Tool approval](https://learn.microsoft.com/agent-framework/agents/tools/tool-approval) |
| Migración | Guía SK a Agent Framework | Entender código antiguo | No | [Guía](https://learn.microsoft.com/agent-framework/migration-guide/from-semantic-kernel/) |
| Protocolo MCP | `ModelContextProtocol` | Integración estándar | No (backlog) | [MCP en .NET](https://learn.microsoft.com/dotnet/ai/get-started-mcp) |
| Identidad de agentes | Microsoft Entra Agent ID | AuthN/AuthZ de agentes | Simulado | [Entra Agent ID](https://learn.microsoft.com/entra/agent-id/what-are-agent-identities) |

Las **evaluaciones** de calidad (existen librerías de Microsoft para ello) y los filtros de contenido de Azure (por ejemplo, para detectar *prompt injection*) se estudiarán en las fases 5 y 6: **(no verificado)** aquí.

## Referencias

- Visión general de las librerías: <https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai>
- Agentes (conceptos): <https://learn.microsoft.com/dotnet/ai/conceptual/agents>
- Agent Framework: <https://learn.microsoft.com/agent-framework/overview/>
