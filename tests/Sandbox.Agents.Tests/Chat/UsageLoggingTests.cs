using System.Net;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Chat;

public sealed class UsageLoggingTests
{
    private const string TestApiKey = "sk-test-secret-key-pike";
    private const string SentText = "mensaje privado del usuario";
    private const string ReceivedText = "respuesta privada del modelo";

    [Fact]
    public async Task GetResponseAsync_WhenCallSucceeds_LogsModelsDurationAndTokensWithoutTexts()
    {
        var logs = new RecordingLoggerProvider();
        using var client = CreateClient(
            new DeepSeekOptions { ApiKey = TestApiKey },
            StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.WithContent(ReceivedText)),
            logs);

        await client.GetResponseAsync(SentText, cancellationToken: TestContext.Current.CancellationToken);

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("deepseek-flash", entry.Properties["RequestedModel"]);
        Assert.Equal("deepseek-flash", entry.Properties["ResponseModel"]);
        Assert.IsType<long>(entry.Properties["ElapsedMilliseconds"]);
        Assert.Equal(13L, entry.Properties["InputTokens"]);
        Assert.Equal(2L, entry.Properties["OutputTokens"]);
        AssertNoSensitiveText(logs);
    }

    [Fact]
    public async Task GetResponseAsync_WhenProviderAnswersWithAnotherModel_LogsWarningWithBothNames()
    {
        var logs = new RecordingLoggerProvider();
        using var client = CreateClient(
            new DeepSeekOptions { ApiKey = TestApiKey, Model = "deepseek-chat" },
            StubHttpMessageHandler.Returning(HttpStatusCode.OK, RecordedResponses.WithContent(ReceivedText, model: "deepseek-flash")),
            logs);

        await client.GetResponseAsync(SentText, cancellationToken: TestContext.Current.CancellationToken);

        var warning = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("deepseek-chat", warning.Message, StringComparison.Ordinal);
        Assert.Contains("deepseek-flash", warning.Message, StringComparison.Ordinal);
        AssertNoSensitiveText(logs);
    }

    [Fact]
    public async Task GetResponseAsync_WhenProviderRejectsKey_LogsNothingWithKeyFragments()
    {
        var logs = new RecordingLoggerProvider();
        using var client = CreateClient(
            new DeepSeekOptions { ApiKey = TestApiKey },
            StubHttpMessageHandler.Returning(HttpStatusCode.Unauthorized, RecordedResponses.InvalidApiKey),
            logs);

        await Assert.ThrowsAsync<ChatProviderException>(
            () => client.GetResponseAsync(SentText, cancellationToken: TestContext.Current.CancellationToken));

        AssertNoSensitiveText(logs);
    }

    private static void AssertNoSensitiveText(RecordingLoggerProvider logs)
    {
        foreach (var entry in logs.Entries)
        {
            var logged = string.Join(" ", [entry.Message, .. entry.Properties.Values.Select(value => value?.ToString()), entry.Exception?.ToString()]);
            Assert.DoesNotContain(SentText, logged, StringComparison.Ordinal);
            Assert.DoesNotContain(ReceivedText, logged, StringComparison.Ordinal);
            Assert.DoesNotContain(TestApiKey, logged, StringComparison.Ordinal);
            Assert.DoesNotContain("pike", logged, StringComparison.Ordinal);
        }
    }

    private static IChatClient CreateClient(DeepSeekOptions options, StubHttpMessageHandler handler, RecordingLoggerProvider logs) =>
        DeepSeekChatClientFactory.Create(options, logs, new HttpClient(handler));
}
