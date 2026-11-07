using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Scripting;

/// <summary>
/// Un turno del modelo con guion: texto final, o peticiones de tools (con un texto opcional que las acompaña), con sus
/// tokens. Por defecto, 100 de entrada y 10 de salida, para que el presupuesto de tokens sea determinista.
/// </summary>
public sealed record ScriptStep(string? Text, IReadOnlyList<ScriptedToolCall> Calls, long InputTokens, long OutputTokens)
{
    public const string ModelId = "scripted-model";

    private const long DefaultInputTokens = 100;

    private const long DefaultOutputTokens = 10;

    public static ScriptStep Reply(string text, long inputTokens = DefaultInputTokens, long outputTokens = DefaultOutputTokens) =>
        new(text, [], inputTokens, outputTokens);

    public static ScriptStep ToolCalls(params (string Name, object Arguments)[] calls) =>
        ToolCallsWithText(null, calls);

    public static ScriptStep ToolCallsWithText(string? text, params (string Name, object Arguments)[] calls)
    {
        ArgumentNullException.ThrowIfNull(calls);
        return new(text, [.. calls.Select(call => Call(call.Name, call.Arguments))], DefaultInputTokens, DefaultOutputTokens);
    }

    public ScriptStep WithTokens(long inputTokens, long outputTokens) =>
        this with { InputTokens = inputTokens, OutputTokens = outputTokens };

    /// <summary>Respuesta del paso, con un identificador nuevo de <paramref name="nextCallId"/> para cada petición.</summary>
    internal ChatResponse ToResponse(Func<string> nextCallId)
    {
        List<AIContent> contents = [];
        if (Text is not null)
        {
            contents.Add(new TextContent(Text));
        }

        contents.AddRange(Calls.Select(call =>
            new FunctionCallContent(nextCallId(), call.Name, call.Arguments.ToDictionary(pair => pair.Key, pair => pair.Value))));
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
        {
            ModelId = ModelId,
            Usage = new UsageDetails
            {
                InputTokenCount = InputTokens,
                OutputTokenCount = OutputTokens,
                TotalTokenCount = InputTokens + OutputTokens,
            },
        };
    }

    private static ScriptedToolCall Call(string name, object arguments)
    {
        var json = JsonSerializer.SerializeToElement(arguments);
        var values = json.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        return new ScriptedToolCall(name, values);
    }
}
