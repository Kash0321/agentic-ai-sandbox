using Microsoft.Extensions.AI;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Telemetry;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Chat;

/// <summary>
/// Decoración común con el modelo con guion (spec <c>agent-scenarios</c>, "Identificación del modelo con guion"; design
/// D1 y D7 de <c>add-scenario-engine</c>).
/// </summary>
[Collection(nameof(TelemetrySerialGroup))]
public sealed class ChatClientPipelineTests
{
    [Fact]
    public async Task GetResponseAsync_WithTheScriptedModel_RecordsAChatSpanWithTheScriptedProviderAndModel()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var client = ChatClientPipeline
            .CreateBuilder(new ScriptedChatClient(ScriptStep.Reply("hola")), new RecordingLoggerProvider())
            .Build();

        await client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken);

        var span = Assert.Single(recorder.Named("chat"));
        Assert.Equal("chat scripted-model", span.DisplayName);
        Assert.Equal("scripted", span.GetTagItem("gen_ai.provider.name"));
        Assert.Equal("scripted-model", span.GetTagItem("gen_ai.request.model"));
        Assert.Equal("scripted-model", span.GetTagItem("gen_ai.response.model"));
    }

    [Fact]
    public async Task GetResponseAsync_WithTheScriptedModel_LogsTheScriptedModelAndItsTokens()
    {
        using var logs = new RecordingLoggerProvider();
        using var client = ChatClientPipeline
            .CreateBuilder(new ScriptedChatClient(ScriptStep.Reply("hola")), logs)
            .Build();

        await client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal("scripted-model", entry.Properties["RequestedModel"]);
        Assert.Equal("scripted-model", entry.Properties["ResponseModel"]);
        Assert.Equal(100L, entry.Properties["InputTokens"]);
        Assert.Equal(10L, entry.Properties["OutputTokens"]);
    }
}
