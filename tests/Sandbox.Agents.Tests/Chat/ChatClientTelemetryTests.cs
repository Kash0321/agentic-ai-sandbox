using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.AI;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Telemetry;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Chat;

/// <summary>
/// Spans del cliente hacia DeepSeek (spec <c>chat-client</c>, "Errores del proveedor sin filtrar la clave", y
/// <c>agent-observability</c>; design D2 y D5 de <c>add-agent-observability</c>).
/// </summary>
[Collection(nameof(TelemetrySerialGroup))]
public sealed class ChatClientTelemetryTests
{
    private const string TestApiKey = "sk-test-secret-key-pike";

    [Fact]
    public async Task GetResponseAsync_WhenProviderRejectsKey_MarksTheSpanAsErrorWithoutKeyFragmentsOrProviderText()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var client = CreateClient(StubHttpMessageHandler.Returning(HttpStatusCode.Unauthorized, RecordedResponses.InvalidApiKey));

        await Assert.ThrowsAsync<ChatProviderException>(
            () => client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken));

        var span = Assert.Single(recorder.Named("chat"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        var text = SpanRecorder.TextOf(span);
        Assert.DoesNotContain("pike", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Authentication Fails", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_WhenProviderRejectsKey_MarksTheSpanAsErrorWithoutKeyFragments()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var client = CreateClient(StubHttpMessageHandler.Returning(HttpStatusCode.Unauthorized, RecordedResponses.InvalidApiKey));

        await Assert.ThrowsAsync<ChatProviderException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });

        var span = Assert.Single(recorder.Named("chat"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.DoesNotContain("pike", SpanRecorder.TextOf(span), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResponseAsync_WhenTheCallSucceeds_RecordsAChatSpanWithModelAndTokens()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var client = CreateClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.HelloWithoutThinking));

        await client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken);

        var span = Assert.Single(recorder.Named("chat"));
        Assert.Equal("deepseek-flash", span.GetTagItem("gen_ai.request.model"));
        Assert.NotNull(span.GetTagItem("gen_ai.response.model"));
        Assert.NotNull(span.GetTagItem(AgentTelemetry.InputTokensTag));
        Assert.NotNull(span.GetTagItem(AgentTelemetry.OutputTokensTag));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetResponseAsync_WithCaptureContent_RecordsTheMessagesOnlyWhenEnabled(bool captureContent)
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var client = CreateClient(
            StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.HelloWithoutThinking),
            new TelemetryOptions { CaptureContent = captureContent });

        await client.GetResponseAsync("mensaje de prueba", cancellationToken: TestContext.Current.CancellationToken);

        var text = SpanRecorder.TextOf(Assert.Single(recorder.Named("chat")));
        Assert.Equal(captureContent, text.Contains("mensaje de prueba", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetResponseAsync_WithCaptureContent_KeepsTheContentOutOfTheLogs()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var logs = new RecordingLoggerProvider();
        using var client = DeepSeekChatClientFactory.Create(
            new DeepSeekOptions { ApiKey = TestApiKey },
            logs,
            new HttpClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.WithContent("respuesta privada"))),
            new TelemetryOptions { CaptureContent = true });

        await client.GetResponseAsync("mensaje de prueba", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("mensaje de prueba", SpanRecorder.TextOf(Assert.Single(recorder.Named("chat"))), StringComparison.Ordinal);
        Assert.NotEmpty(logs.Entries);
        var logged = string.Join("\n", logs.Entries.Select(entry => $"{entry.Message} {string.Join(" ", entry.Properties.Values)}"));
        Assert.DoesNotContain("mensaje de prueba", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("respuesta privada", logged, StringComparison.Ordinal);
    }

    private static IChatClient CreateClient(StubHttpMessageHandler handler, TelemetryOptions? telemetry = null) =>
        DeepSeekChatClientFactory.Create(
            new DeepSeekOptions { ApiKey = TestApiKey },
            new RecordingLoggerProvider(),
            new HttpClient(handler),
            telemetry);
}
