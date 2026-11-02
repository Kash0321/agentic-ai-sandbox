using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.FaultInjection;
using Sandbox.Application.Sales;
using Sandbox.Domain.Sales;
using Sandbox.Runner;

namespace Sandbox.Scenarios.Tests.Runner;

[Collection(nameof(RunnerHostSerialGroup))]
public sealed class RunnerAgentTests : IDisposable
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
    public async Task RunAsync_WithHappyOrderScenario_CreatesTheOrderAndPrintsAnswerAndSummary()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)),
            ScriptStep.Reply("Pedido ORD-0001 creado"));

        var exitCode = await RunAsync(model, ValidKeyArgument, "--scenario", "happy-order");

        Assert.Equal(ExitCodes.Success, exitCode);
        var output = _output.ToString();
        Assert.Contains("Respuesta: Pedido ORD-0001 creado", output, StringComparison.Ordinal);
        Assert.Contains("Llamadas al modelo: 3", output, StringComparison.Ordinal);
        Assert.Contains("Tools: list_products (OK), place_order (OK)", output, StringComparison.Ordinal);
        Assert.Contains("Tokens: 300 de entrada y 30 de salida", output, StringComparison.Ordinal);
        var order = await new SalesQueries(_state).GetOrderAsync(new GetOrderQuery("ORD-0001"), TestContext.Current.CancellationToken);
        Assert.Equal(OrderStatus.Placed, order.Value?.Status);
        Assert.Equal("CUST-001", order.Value?.CustomerId);
        Assert.Equal(18, (await new SalesQueries(_state).GetStockAsync(new GetStockQuery("SKU-MUG-RED"), TestContext.Current.CancellationToken)).Value?.Available);
        Assert.Equal("Hola, quiero pedir dos tazas rojas, por favor.", model.Requests[0][1].Text);
    }

    [Fact]
    public async Task RunAsync_WithLostResponseScenario_RetriesWithTheSameKeyAndCreatesASingleOrder()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)),
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)),
            ScriptStep.Reply("Pedido ORD-0001 creado"));

        var exitCode = await RunAsync(model, ValidKeyArgument, "--scenario", "lost-response");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains(
            "Tools: list_products (OK), place_order (SERVICE_UNAVAILABLE), place_order (OK)",
            _output.ToString(),
            StringComparison.Ordinal);
        var lost = Assert.IsType<JsonElement>(Assert.Single(ResultsSentIn(model.Requests[2])).Result);
        Assert.True(lost.GetProperty("error").GetProperty("retryable").GetBoolean());
        var queries = new SalesQueries(_state);
        Assert.True((await queries.GetOrderAsync(new GetOrderQuery("ORD-0001"), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.False((await queries.GetOrderAsync(new GetOrderQuery("ORD-0002"), TestContext.Current.CancellationToken)).IsSuccess);
    }

    [Fact]
    public async Task RunAsync_WithPromptAndAnotherCustomer_ActsForThatCustomer()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.GetCustomer, new { })),
            ScriptStep.Reply("Eres Bruno"));

        var exitCode = await RunAsync(model, ValidKeyArgument, "--prompt", "¿quién soy?", "--customer", "CUST-002");

        Assert.Equal(ExitCodes.Success, exitCode);
        var customer = Assert.IsType<JsonElement>(Assert.Single(model.ReceivedToolResults).Result);
        Assert.Equal("CUST-002", customer.GetProperty("data").GetProperty("customerId").GetString());
        Assert.Equal("¿quién soy?", model.Requests[0][1].Text);
    }

    [Fact]
    public async Task RunAsync_WhenTheProviderRejectsTheKeyDuringTheAgent_ReturnsModelCallFailed()
    {
        var chatClient = FakeChatClient.Throwing(new ChatProviderException(ChatProviderErrorCategory.Authentication, 401));

        var exitCode = await RunAsync(chatClient, ValidKeyArgument, "--scenario", "happy-order");

        Assert.Equal(ExitCodes.ModelCallFailed, exitCode);
    }

    [Theory]
    [InlineData("Agent:Scenario", "--scenario", "no-existe")]
    [InlineData("Agent:Prompt", "--prompt", "   ")]
    [InlineData("Agent:Scenario", "--scenario", "happy-order", "--prompt", "hola")]
    [InlineData("Agent:CustomerId", "--prompt", "hola", "--customer", " ")]
    public async Task RunAsync_WithInvalidAgentArguments_ReturnsInvalidConfigurationWithoutCallingTheModel(
        string namedOption,
        params string[] arguments)
    {
        using var model = new ScriptedChatClient(ScriptStep.Reply("no debería llamarse"));

        var exitCode = await RunAsync(model, [ValidKeyArgument, .. arguments]);

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains(namedOption, _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public void CreateBuilder_WithLostResponseScenario_AddsItsFaultAfterTheConfiguredRules()
    {
        using var host = RunnerHost.CreateBuilder(
            [
                ValidKeyArgument,
                "--scenario", "lost-response",
                "--FaultInjection:Rules:0:Operation=GetStock",
                "--FaultInjection:Rules:0:Trigger=Always",
            ],
            TemporarySecretsDirectory.Missing()).Build();

        var rules = host.Services.GetRequiredService<IOptions<FaultInjectionOptions>>().Value.Rules;

        Assert.Equal(2, rules.Count);
        Assert.Equal(FaultOperations.GetStock, rules[0].Operation);
        Assert.Equal((FaultOperations.PlaceOrder, FaultTiming.After), (rules[1].Operation, rules[1].Timing));
    }

    [Theory]
    [InlineData("CUST-002", "--scenario", "happy-order", "--customer", "CUST-002")]
    [InlineData("CUST-001", "--scenario", "happy-order")]
    [InlineData("CUST-001", "--prompt", "hola")]
    [InlineData("CUST-003", "--prompt", "hola", "--customer", "CUST-003")]
    public void CreateBuilder_WithAgentArguments_ResolvesTheSessionCustomerByPreference(string expected, params string[] arguments)
    {
        using var host = RunnerHost.CreateBuilder([ValidKeyArgument, .. arguments], TemporarySecretsDirectory.Missing()).Build();

        Assert.Equal(expected, host.Services.GetRequiredService<SalesSession>().CustomerId);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _logs.Dispose();
    }

    private static IEnumerable<FunctionResultContent> ResultsSentIn(IReadOnlyList<ChatMessage> request) =>
        request.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Skip(1);

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
