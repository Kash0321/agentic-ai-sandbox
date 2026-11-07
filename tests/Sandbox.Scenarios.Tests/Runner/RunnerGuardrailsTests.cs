using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.Auditing;
using Sandbox.Application.Sales;
using Sandbox.Runner;
using Sandbox.Runner.Agent;
using Sandbox.Runner.Scenarios;

namespace Sandbox.Scenarios.Tests.Runner;

/// <summary>Guardrails en el Runner: configuración, escenario de solo lectura, código 4 y salida (specs <c>runner-host</c>, <c>agent-guardrails</c> y <c>sales-agent</c>).</summary>
[Collection(nameof(RunnerHostSerialGroup))]
public sealed class RunnerGuardrailsTests : IDisposable
{
    private const string ValidKeyArgument = "--DeepSeek:ApiKey=sk-test";

    private static readonly object TwoMugs = new
    {
        lines = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } },
        idempotencyKey = "tazas-rojas-1",
    };

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly RecordingLoggerProvider _logs = new();
    private readonly SalesState _state = SeedCatalog.CreateState();

    [Fact]
    public void CreateBuilder_WithoutGuardrailsSection_UsesTheDefaultLimitsAndTheSixTools()
    {
        var guardrails = GuardrailsFor(ValidKeyArgument, "--scenario", "happy-order");

        Assert.Equal((10, 3, 20_000), (guardrails.MaxModelCalls, guardrails.MaxConsecutiveToolErrors, guardrails.TokenBudget));
        Assert.Equal(SalesToolNames.All, guardrails.EffectiveAllowedTools);
    }

    [Fact]
    public void CreateBuilder_WithReadOnlyScenario_AllowsOnlyTheReadTools()
    {
        var guardrails = GuardrailsFor(ValidKeyArgument, "--scenario", "read-only");

        Assert.Equal(SalesToolNames.ReadOnly, guardrails.EffectiveAllowedTools);
    }

    [Fact]
    public async Task RunAsync_WithReadOnlyScenarioAndANarrowerAllowList_OffersOnlyTheConfiguredTool()
    {
        using var model = new ScriptedChatClient(ScriptStep.Reply("Hola"));

        var exitCode = await RunAsync(model, ValidKeyArgument, "--scenario", "read-only", "--Guardrails:AllowedTools:0=list_products");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(["list_products"], (model.RequestOptions[0]?.Tools ?? []).Select(tool => tool.Name));
    }

    [Theory]
    [InlineData("Guardrails:MaxModelCalls", "--Guardrails:MaxModelCalls=0")]
    [InlineData("Guardrails:TokenBudget", "--Guardrails:TokenBudget=0")]
    [InlineData("Guardrails:AllowedTools", "--Guardrails:AllowedTools:0=ship_order")]
    [InlineData("Guardrails:AllowedTools", "--Guardrails:AllowedTools:0=place_order", "--scenario", "read-only")]
    public async Task RunAsync_WithInvalidGuardrails_ReturnsInvalidConfigurationWithoutCallingTheModel(
        string namedSetting,
        params string[] arguments)
    {
        using var model = new ScriptedChatClient(ScriptStep.Reply("no debería llamarse"));
        string[] scenario = arguments.Contains("--scenario") ? [] : ["--scenario", "happy-order"];

        var exitCode = await RunAsync(model, [ValidKeyArgument, .. scenario, .. arguments]);

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains(namedSetting, _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task RunAsync_StoppedByAGuardrail_ReturnsFourAndNamesTheReason()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })));

        var exitCode = await RunAsync(model, ValidKeyArgument, "--scenario", "happy-order", "--Guardrails:MaxModelCalls=1");

        Assert.Equal(ExitCodes.StoppedByGuardrail, exitCode);
        Assert.Contains("ModelCallLimit", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Motivo de parada: ModelCallLimit", _output.ToString(), StringComparison.Ordinal);
        Assert.Single(model.Requests);
    }

    [Fact]
    public async Task RunAsync_WithReadOnlyScenario_BlocksPlaceOrderAndCreatesNoOrder()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)),
            ScriptStep.Reply("No puedo hacer pedidos en esta conversación"));

        var exitCode = await RunAsync(model, ValidKeyArgument, "--scenario", "read-only");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Tools: list_products (OK), place_order (BLOCKED_BY_POLICY)", _output.ToString(), StringComparison.Ordinal);
        Assert.Equal(SalesToolNames.ReadOnly, (model.RequestOptions[0]?.Tools ?? []).Select(tool => tool.Name));
        var queries = new SalesQueries(_state, new AuditLog(NullLogger.Instance));
        var token = TestContext.Current.CancellationToken;
        Assert.False((await queries.GetOrderAsync(new GetOrderQuery("ORD-0001", "CUST-001"), token)).IsSuccess);
        Assert.Equal(20, (await queries.GetStockAsync(new GetStockQuery("SKU-MUG-RED"), token)).Value?.Available);
    }

    [Fact]
    public async Task RunAsync_DuringAnAgentRun_LogsEveryAuditEventWithTheRunId()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
            ScriptStep.Reply("Aquí tienes el catálogo"));

        await RunAsync(model, ValidKeyArgument, "--scenario", "happy-order");

        var audit = Assert.Single(_logs.Entries, entry => entry.Category == AuditLog.Category);
        Assert.False(string.IsNullOrEmpty(audit.Scopes["RunId"]?.ToString()));
        Assert.Equal("CUST-001", audit.Scopes["CustomerId"]);
    }

    [Fact]
    public void CreateBuilder_Always_ShowsScopesInTheConsole()
    {
        using var host = RunnerHost.CreateBuilder([ValidKeyArgument], TemporarySecretsDirectory.Missing()).Build();

        // Las dos cosas hacen falta: con el nombre del formateador vacío, la consola ignora sus opciones (apply).
        Assert.Equal(ConsoleFormatterNames.Simple, host.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value.FormatterName);
        Assert.True(host.Services.GetRequiredService<IOptionsMonitor<SimpleConsoleFormatterOptions>>().CurrentValue.IncludeScopes);
    }

    [Fact]
    public async Task RunAsync_WithAPromptLongerThanTheMaximum_ReturnsInvalidConfigurationNamingThePrompt()
    {
        using var model = new ScriptedChatClient(ScriptStep.Reply("no debería llamarse"));

        var exitCode = await RunAsync(model, ValidKeyArgument, "--prompt", new string('a', SalesAgent.MaxRequestLength + 1));

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("Agent:Prompt", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public void ScenarioCatalog_Always_HasRequestsWithinTheMaximumLength()
    {
        Assert.All(ScenarioCatalog.All, scenario => Assert.InRange(scenario.Request.Length, 1, SalesAgent.MaxRequestLength));
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _logs.Dispose();
    }

    private static GuardrailOptions GuardrailsFor(params string[] args)
    {
        using var host = RunnerHost.CreateBuilder(args, TemporarySecretsDirectory.Missing()).Build();
        return host.Services.GetRequiredService<IOptions<GuardrailOptions>>().Value;
    }

    private async Task<int> RunAsync(IChatClient chatClient, params string[] args)
    {
        var builder = RunnerHost.CreateBuilder(args, TemporarySecretsDirectory.Missing());
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_logs);
        builder.Services.AddSingleton(chatClient);
        builder.Services.AddSingleton(_state);

        return await RunnerHost.RunAsync(builder, _output, _error, TestContext.Current.CancellationToken);
    }
}
