using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Runner;

namespace Sandbox.Scenarios.Tests.Runner;

[Collection(nameof(RunnerHostSerialGroup))]
public sealed class RunnerExitCodeTests : IDisposable
{
    private const string ValidKeyArgument = "--DeepSeek:ApiKey=sk-test-secret-key-pike";

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly RecordingLoggerProvider _logs = new();

    [Fact]
    public async Task RunAsync_WithFakeGreeting_PrintsTextModelAndTokensAndReturnsSuccess()
    {
        var chatClient = FakeChatClient.RespondingWith("hola", modelId: "fake-flash", inputTokens: 13, outputTokens: 2);

        var exitCode = await RunAsync(chatClient, ValidKeyArgument);

        Assert.Equal(ExitCodes.Success, exitCode);
        var output = _output.ToString();
        Assert.Contains("hola", output, StringComparison.Ordinal);
        Assert.Contains("fake-flash", output, StringComparison.Ordinal);
        Assert.Contains("13", output, StringComparison.Ordinal);
        Assert.Contains("2", output, StringComparison.Ordinal);
        Assert.Equal(GreetingRunner.GreetingPrompt, Assert.Single(chatClient.ReceivedMessages).Text);
    }

    [Fact]
    public async Task RunAsync_WithoutApiKey_ReturnsInvalidConfigurationNamingTheOption()
    {
        var chatClient = FakeChatClient.RespondingWith("hola");

        var exitCode = await RunAsync(chatClient);

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("DeepSeek:ApiKey", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(chatClient.ReceivedMessages);
        Assert.Empty(_output.ToString());
        Assert.DoesNotContain(_logs.Entries, entry => entry.Exception is not null);
    }

    [Fact]
    public async Task RunAsync_WhenProviderRejectsKey_ReturnsModelCallFailedWithoutKeyFragments()
    {
        var chatClient = FakeChatClient.Throwing(new ChatProviderException(ChatProviderErrorCategory.Authentication, 401));

        var exitCode = await RunAsync(chatClient, ValidKeyArgument);

        Assert.Equal(ExitCodes.ModelCallFailed, exitCode);
        var error = _error.ToString();
        Assert.Contains("autenticación", error, StringComparison.Ordinal);
        Assert.Contains("401", error, StringComparison.Ordinal);
        Assert.DoesNotContain("pike", error + _output + LoggedText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenUnexpectedErrorOccurs_ReturnsUnexpectedErrorWithoutStackTrace()
    {
        var failure = new InvalidOperationException("detalle interno del fallo");
        var chatClient = FakeChatClient.Throwing(failure);

        var exitCode = await RunAsync(chatClient, ValidKeyArgument);

        Assert.Equal(ExitCodes.UnexpectedError, exitCode);
        var error = _error.ToString();
        Assert.DoesNotContain("detalle interno", error, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", error, StringComparison.Ordinal);
        var logged = Assert.Single(_logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Same(failure, logged.Exception);
    }

    private async Task<int> RunAsync(IChatClient chatClient, params string[] args)
    {
        var builder = RunnerHost.CreateBuilder(args, TemporarySecretsDirectory.Missing());
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_logs);
        builder.Services.AddSingleton(chatClient);

        return await RunnerHost.RunAsync(builder, _output, _error, TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _logs.Dispose();
    }

    private string LoggedText() =>
        string.Join(" ", _logs.Entries.Select(entry => $"{entry.Message} {entry.Exception}"));
}
