# 4. Herramientas de Microsoft: qué problema resuelven y cómo

> **Estado**: documento base. **Verificado**: 2026-10-31 contra Microsoft Learn. Versiones vistas en la documentación: `Microsoft.Extensions.AI.Abstractions` 10.10.x, `Microsoft.Agents.AI` 1.13.0. **Revisar versiones en la fase 1** al fijar `Directory.Packages.props`.

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

- **`AIFunctionFactory.Create(método)`** genera una `AIFunction` a partir de un método .NET. El nombre, la descripción (`[Description]`) y el schema salen de la firma.
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

**Cómo**: método de extensión `AsIChatClient()` sobre un `ChatClient` de OpenAI. Como DeepSeek expone una API compatible con OpenAI, se usa este mismo adaptador apuntando el cliente a otro *endpoint* (la clave se configura en el cliente OpenAI). **(A verificar en la fase 2)** la forma exacta de fijar el endpoint y los límites de compatibilidad de DeepSeek.

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
