using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Tools;

namespace Sandbox.Agents.Agent;

/// <summary>
/// Agente de ventas: envía al modelo las instrucciones, la petición y las tools, y ejecuta las tools que pide hasta que
/// responde con texto (design D7). El bucle es el <see cref="FunctionInvokingChatClient"/> de Microsoft.Extensions.AI,
/// con sus límites por defecto; su <c>FunctionInvoker</c> es el borde exterior de las tools (design D3).
/// </summary>
public sealed partial class SalesAgent(
    IChatClient chatClient,
    SalesTools tools,
    IOptions<DeepSeekOptions> deepSeekOptions,
    ILogger<SalesAgent> logger)
{
    public async Task<AgentRunResult> RunAsync(string request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);

        if (deepSeekOptions.Value.Thinking)
        {
            LogThinkingWithTools(logger);
        }

        List<ToolInvocation> invocations = [];
        using var counter = new CallCountingChatClient(chatClient);
        using var loop = new FunctionInvokingChatClient(counter)
        {
            // La spec exige ejecutar en serie las tools de una misma respuesta. Es el valor por defecto en la 10.11.0,
            // pero se fija para que una versión futura no lo cambie en silencio.
            AllowConcurrentInvocation = false,
            FunctionInvoker = (context, token) => InvokeToolAsync(context, invocations, token),
        };

        List<ChatMessage> messages =
        [
            new(ChatRole.System, SystemPrompt.Text),
            new(ChatRole.User, request),
        ];
        var options = new ChatOptions { Tools = [.. tools.CreateFunctions()] };

        var response = await loop.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        var assistantTexts = response.Messages
            .Where(message => message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
            .Select(message => message.Text)
            .ToList();
        foreach (var intermediate in assistantTexts.SkipLast(1))
        {
            LogIntermediateText(logger, intermediate);
        }

        return new AgentRunResult(
            assistantTexts.LastOrDefault() ?? string.Empty,
            counter.Calls,
            invocations,
            response.Usage?.InputTokenCount ?? 0,
            response.Usage?.OutputTokenCount ?? 0);
    }

    private async ValueTask<object?> InvokeToolAsync(
        FunctionInvocationContext context,
        List<ToolInvocation> invocations,
        CancellationToken cancellationToken)
    {
        var toolName = context.Function.Name;
        var stopwatch = Stopwatch.StartNew();
        object? result;

        try
        {
            result = await context.Function.InvokeAsync(context.Arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            // El borde interior de cada tool captura lo inesperado de su cuerpo: lo que llega aquí es la asignación de
            // argumentos que no cumplen el esquema, antes de ejecutar la tool. No se reenvía el mensaje interno.
            result = JsonSerializer.SerializeToElement(
                ToolResult.Failure<object>(ToolError.InvalidArguments(toolName)),
                ToolJson.SerializerOptions);
        }

        var outcome = OutcomeOf(result);
        invocations.Add(new ToolInvocation(toolName, outcome));
        LogToolInvocation(logger, toolName, outcome, stopwatch.ElapsedMilliseconds);
        return result;
    }

    private static string OutcomeOf(object? result) =>
        result is JsonElement { ValueKind: JsonValueKind.Object } json
        && json.TryGetProperty("error", out var error)
        && error.TryGetProperty("code", out var code)
            ? code.GetString() ?? ToolErrorCodes.InternalError
            : ToolInvocation.Succeeded;

    [LoggerMessage(Level = LogLevel.Information, Message = "Tool {Tool}: {Outcome} en {ElapsedMs} ms")]
    private static partial void LogToolInvocation(ILogger logger, string tool, string outcome, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Texto intermedio del modelo: {Text}")]
    private static partial void LogIntermediateText(ILogger logger, string text);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Razonamiento activado con tools: DeepSeek documenta que reasoning_content debe reenviarse en los " +
            "turnos con tools, y esta solución no lo reenvía. En las pruebas del 2026-11-01 la API respondió igualmente.")]
    private static partial void LogThinkingWithTools(ILogger logger);
}
