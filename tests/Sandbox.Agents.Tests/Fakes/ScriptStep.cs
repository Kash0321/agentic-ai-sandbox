using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Un turno del modelo falso: texto final, o llamadas a tools (con un texto opcional que las acompaña), con sus tokens.
/// Los argumentos se convierten a <see cref="JsonElement"/>, como los entrega el adaptador real.
/// </summary>
internal sealed record ScriptStep(string? Text, IReadOnlyList<FunctionCallContent> Calls, long InputTokens, long OutputTokens)
{
    private static int _callCounter;

    public static ScriptStep Reply(string text, long inputTokens = 100, long outputTokens = 10) =>
        new(text, [], inputTokens, outputTokens);

    public static ScriptStep ToolCalls(params (string Name, object Arguments)[] calls) =>
        ToolCallsWithText(null, calls);

    public static ScriptStep ToolCallsWithText(string? text, params (string Name, object Arguments)[] calls) =>
        new(text, [.. calls.Select(call => Call(call.Name, call.Arguments))], 100, 10);

    public ScriptStep WithTokens(long inputTokens, long outputTokens) =>
        this with { InputTokens = inputTokens, OutputTokens = outputTokens };

    public ChatResponse ToResponse()
    {
        List<AIContent> contents = [];
        if (Text is not null)
        {
            contents.Add(new TextContent(Text));
        }

        contents.AddRange(Calls);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
        {
            ModelId = "scripted-model",
            Usage = new UsageDetails
            {
                InputTokenCount = InputTokens,
                OutputTokenCount = OutputTokens,
                TotalTokenCount = InputTokens + OutputTokens,
            },
        };
    }

    private static FunctionCallContent Call(string name, object arguments)
    {
        var json = JsonSerializer.SerializeToElement(arguments);
        var values = json.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        return new FunctionCallContent($"call_{Interlocked.Increment(ref _callCounter)}", name, values);
    }
}
