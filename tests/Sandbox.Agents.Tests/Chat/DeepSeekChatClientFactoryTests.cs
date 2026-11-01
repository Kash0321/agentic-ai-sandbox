using System.Net;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Chat;

public sealed class DeepSeekChatClientFactoryTests
{
    private const string TestApiKey = "sk-test-secret-key-1234";

    [Fact]
    public async Task GetResponseAsync_WithDefaultOptions_SendsFlashModelAndDisablesThinking()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.HelloWithoutThinking);
        using var client = CreateClient(new DeepSeekOptions { ApiKey = TestApiKey }, handler);

        await client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("deepseek-flash", request.RootElement.GetProperty("model").GetString());
        Assert.Equal("disabled", request.RootElement.GetProperty("thinking").GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetResponseAsync_WithThinkingEnabled_DoesNotSendThinkingAndSeparatesReasoning()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.HelloWithThinking);
        using var client = CreateClient(new DeepSeekOptions { ApiKey = TestApiKey, Thinking = true }, handler);

        var response = await client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.False(request.RootElement.TryGetProperty("thinking", out _));
        Assert.Equal("hola", response.Text);
        var reasoning = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<TextReasoningContent>());
        Assert.Equal("We need answer only word hola. Need obey.", reasoning.Text);
    }

    [Fact]
    public async Task GetResponseAsync_WithCallerRawRepresentation_KeepsItAndDisablesThinking()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.HelloWithoutThinking);
        using var client = CreateClient(new DeepSeekOptions { ApiKey = TestApiKey }, handler);
        var options = new ChatOptions
        {
            RawRepresentationFactory = _ => new OpenAI.Chat.ChatCompletionOptions { EndUserId = "caller-option" },
        };

        await client.GetResponseAsync("hola", options, TestContext.Current.CancellationToken);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("caller-option", request.RootElement.GetProperty("user").GetString());
        Assert.Equal("disabled", request.RootElement.GetProperty("thinking").GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetResponseAsync_WithValidOptions_PostsToChatCompletionsWithBearerKey()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.HelloWithoutThinking);
        using var client = CreateClient(new DeepSeekOptions { ApiKey = TestApiKey }, handler);

        await client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://api.deepseek.com/chat/completions"), handler.RequestUri);
        // Se compara sin Assert.Equal para que un fallo no imprima la cabecera.
        Assert.True(handler.Authorization == $"Bearer {TestApiKey}", "La cabecera Authorization no es la esperada.");
    }

    [Fact]
    public async Task GetResponseAsync_WithRecordedResponse_ReturnsModelAndUsage()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.HelloWithoutThinking);
        using var client = CreateClient(new DeepSeekOptions { ApiKey = TestApiKey }, handler);

        var response = await client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("hola", response.Text);
        Assert.Equal("deepseek-flash", response.ModelId);
        Assert.Equal(13, response.Usage?.InputTokenCount);
        Assert.Equal(2, response.Usage?.OutputTokenCount);
    }

    private static IChatClient CreateClient(DeepSeekOptions options, StubHttpMessageHandler handler) =>
        DeepSeekChatClientFactory.Create(options, new RecordingLoggerProvider(), new HttpClient(handler));
}
