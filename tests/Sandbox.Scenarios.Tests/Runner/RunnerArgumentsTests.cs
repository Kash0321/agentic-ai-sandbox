using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Runner;
using Sandbox.Runner.Agent;

namespace Sandbox.Scenarios.Tests.Runner;

/// <summary>
/// Opción <c>--model</c>, suborden <c>list</c> y argumentos posicionales (spec <c>runner-host</c>, "Argumentos de
/// ejecución inválidos"; design D6 de <c>add-scenario-engine</c>).
/// </summary>
[Collection(nameof(RunnerHostSerialGroup))]
public sealed class RunnerArgumentsTests : IDisposable
{
    private const string ValidKeyArgument = "--DeepSeek:ApiKey=sk-test";

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly RecordingLoggerProvider _logs = new();

    [Theory]
    [InlineData(new[] { "list" }, new[] { "list" })]
    [InlineData(new[] { "--model", "scripted", "list" }, new[] { "list" })]
    [InlineData(new[] { "list", "--model", "scripted" }, new[] { "list" })]
    [InlineData(new[] { "--customer", "CUST-002", "list", "--model=scripted" }, new[] { "list" })]
    [InlineData(new[] { "--DeepSeek:ApiKey=sk-test", "list" }, new[] { "list" })]
    [InlineData(new[] { "--scenario", "list" }, new string[0])]
    [InlineData(new[] { "--prompt", "hola mundo", "--model", "deepseek" }, new string[0])]
    [InlineData(new[] { "lsit", "otro" }, new[] { "lsit", "otro" })]
    public void PositionalArguments_WithSwitchesAndValues_ReturnsOnlyThePositionalOnes(string[] args, string[] expected)
    {
        Assert.Equal(expected, AgentOptions.PositionalArguments(args));
    }

    [Theory]
    [InlineData("Agent:ModelSource", "--scenario", "happy-order", "--model", "gpt")]
    [InlineData("necesita un escenario", "--model", "scripted", "--prompt", "hola")]
    [InlineData("necesita un escenario", "--model", "scripted")]
    [InlineData("lsit", "lsit")]
    [InlineData("list no admite", "list", "--scenario", "happy-order")]
    [InlineData("list no admite", "--prompt", "hola", "list")]
    [InlineData("happy-order, lost-response, read-only, cancel-order", "--scenario", "no-existe")]
    public async Task RunAsync_WithInvalidArguments_ReturnsInvalidConfigurationWithoutCallingTheModel(
        string expectedMessage,
        params string[] arguments)
    {
        using var model = new ScriptedChatClient(ScriptStep.Reply("no debería llamarse"));

        var exitCode = await RunAsync(model, [ValidKeyArgument, .. arguments]);

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains(expectedMessage, _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Theory]
    [InlineData("SCRIPTED")]
    [InlineData("DeepSeek")]
    public async Task RunAsync_WithTheModelInAnyCase_AcceptsIt(string model)
    {
        using var chatClient = new ScriptedChatClient(ScriptStep.Reply("hola"));

        var exitCode = await RunAsync(chatClient, ValidKeyArgument, "--scenario", "read-only", "--model", model);

        Assert.NotEqual(ExitCodes.InvalidConfiguration, exitCode);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _logs.Dispose();
    }

    private async Task<int> RunAsync(ScriptedChatClient chatClient, params string[] args)
    {
        var builder = RunnerHost.CreateBuilder(args, TemporarySecretsDirectory.Missing());
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_logs);
        builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(chatClient);

        return await RunnerHost.RunAsync(builder, _output, _error, TestContext.Current.CancellationToken);
    }
}
