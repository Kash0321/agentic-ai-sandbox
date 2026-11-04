using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tests.Tools;
using Sandbox.Agents.Tools;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;

namespace Sandbox.Agents.Tests.Agent;

public sealed class SalesAgentTests
{
    private static readonly object TwoMugs = new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } }, idempotencyKey = "k1" };

    [Fact]
    public async Task RunAsync_WithListThenPlaceThenReply_CreatesTheOrderInThreeModelCalls()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)),
            ScriptStep.Reply("Pedido ORD-0001 creado")));

        var result = await harness.RunAsync();

        Assert.Equal(3, result.ModelCalls);
        Assert.Equal(
            [new ToolInvocation("list_products", "OK"), new ToolInvocation("place_order", "OK")],
            result.ToolInvocations);
        Assert.Equal("Pedido ORD-0001 creado", result.FinalAnswer);
        Assert.Contains(ToolResults(harness.Model.Requests[1]), content => content.CallId.Length > 0);
        Assert.Equal(18, (await harness.Tools.Queries.GetStockAsync(new GetStockQuery("SKU-MUG-RED"), TestContext.Current.CancellationToken)).Value?.Available);
    }

    [Fact]
    public async Task RunAsync_WithTwoToolsInOneResponse_RunsBothAndSendsBothResults()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { }), (SalesToolNames.GetCustomer, new { })),
            ScriptStep.Reply("Hola, Ana")));

        var result = await harness.RunAsync();

        Assert.Equal(["list_products", "get_customer"], result.ToolInvocations.Select(invocation => invocation.Name));
        Assert.Equal(2, ToolResults(harness.Model.Requests[1]).Count);
        Assert.Equal(2, result.ModelCalls);
    }

    [Fact]
    public async Task RunAsync_WithTextAlongsideAToolCall_KeepsItOutOfTheFinalAnswerAndLogsIt()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCallsWithText("Voy a buscar el producto", (SalesToolNames.ListProducts, new { })),
            ScriptStep.Reply("Pedido ORD-0001 creado")));

        var result = await harness.RunAsync();

        Assert.Equal("Pedido ORD-0001 creado", result.FinalAnswer);
        Assert.Contains("Voy a buscar el producto", harness.LoggedText, StringComparison.Ordinal);
        Assert.Equal(2, result.ModelCalls);
        Assert.Equal([new ToolInvocation("list_products", "OK")], result.ToolInvocations);
        Assert.Equal(AgentStopReason.Completed, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_WithThreeModelCalls_AddsUpTheirTokens()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })).WithTokens(100, 10),
            ScriptStep.ToolCalls((SalesToolNames.GetCustomer, new { })).WithTokens(200, 20),
            ScriptStep.Reply("fin", inputTokens: 300, outputTokens: 30)));

        var result = await harness.RunAsync();

        Assert.Equal(600, result.InputTokens);
        Assert.Equal(60, result.OutputTokens);
    }

    [Fact]
    public async Task RunAsync_WhenARequiredParameterIsMissing_ReturnsInvalidArgumentsWithoutRunningTheTool()
    {
        // Si la tool llegara a ejecutarse, estas queries lanzarían y el resultado sería INTERNAL_ERROR.
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.GetStock, new { })), ScriptStep.Reply("fin")),
            queries: new ThrowingQueries(new InvalidOperationException("no debería consultarse")));

        var result = await harness.RunAsync();

        Assert.Equal([new ToolInvocation("get_stock", ToolErrorCodes.InvalidArguments)], result.ToolInvocations);
        var sent = Assert.IsType<JsonElement>(Assert.Single(ToolResults(harness.Model.Requests[1])).Result);
        var error = sent.GetProperty("error");
        Assert.Equal(ToolErrorCodes.InvalidArguments, error.GetProperty("code").GetString());
        Assert.False(error.GetProperty("retryable").GetBoolean());
        Assert.Contains("get_stock", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Tools.Logs.Entries, entry => entry.Exception is not null);
    }

    [Fact]
    public async Task RunAsync_WhenTheApplicationThrows_SendsInternalErrorAndLogsTheException()
    {
        var failure = new InvalidOperationException("detalle interno");
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })), ScriptStep.Reply("fin")),
            queries: new ThrowingQueries(failure));

        var result = await harness.RunAsync();

        Assert.Equal([new ToolInvocation("list_products", ToolErrorCodes.InternalError)], result.ToolInvocations);
        var sent = Assert.IsType<JsonElement>(Assert.Single(ToolResults(harness.Model.Requests[1])).Result);
        Assert.DoesNotContain("detalle interno", sent.GetRawText(), StringComparison.Ordinal);
        Assert.Same(failure, Assert.Single(harness.Tools.Logs.Entries, entry => entry.Exception is not null).Exception);
    }

    [Fact]
    public async Task RunAsync_CancelledWhileAToolRuns_PropagatesTheCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })), ScriptStep.Reply("fin")),
            queries: new CancellingQueries(cancellation));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Agent.RunAsync("Quiero dos tazas rojas", cancellation.Token));

        Assert.Single(harness.Model.Requests);
    }

    [Fact]
    public async Task RunAsync_WhenAToolFails_LogsNameDurationAndCodeWithoutArguments()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, new
            {
                lines = new[] { new { sku = "SKU-TSHIRT-M", quantity = 6 } },
                idempotencyKey = "clave-secreta-123",
            })),
            ScriptStep.Reply("fin")));

        await harness.RunAsync();

        var entry = Assert.Single(harness.Tools.Logs.Entries, entry => Equals(entry.Properties.GetValueOrDefault("Tool"), "place_order"));
        Assert.Equal(ErrorCodes.InsufficientStock, entry.Properties["Outcome"]);
        Assert.True(entry.Properties.ContainsKey("ElapsedMs"));
        Assert.DoesNotContain("SKU-TSHIRT-M", harness.LoggedText, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-secreta-123", harness.LoggedText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task RunAsync_DependingOnThinking_WarnsAboutReasoningWithTools(bool thinking, int expectedWarnings)
    {
        using var harness = new AgentHarness(new ScriptedChatClient(ScriptStep.Reply("hola")), thinking);

        var result = await harness.RunAsync();

        Assert.Equal("hola", result.FinalAnswer);
        Assert.Equal(
            expectedWarnings,
            harness.Tools.Logs.Entries.Count(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("reasoning_content", StringComparison.Ordinal)));
    }

    private static List<FunctionResultContent> ToolResults(IReadOnlyList<ChatMessage> request) =>
        [.. request.SelectMany(message => message.Contents).OfType<FunctionResultContent>()];

    /// <summary>Queries que cancelan la ejecución desde dentro de una tool, como lo haría quien la lanzó.</summary>
    private sealed class CancellingQueries(CancellationTokenSource cancellation) : ISalesQueries
    {
        public Task<Result<OrderView>> GetOrderAsync(GetOrderQuery query, CancellationToken cancellationToken = default) =>
            Cancel<OrderView>(cancellationToken);

        public Task<Result<CustomerView>> GetCustomerAsync(GetCustomerQuery query, CancellationToken cancellationToken = default) =>
            Cancel<CustomerView>(cancellationToken);

        public Task<Result<StockView>> GetStockAsync(GetStockQuery query, CancellationToken cancellationToken = default) =>
            Cancel<StockView>(cancellationToken);

        public Task<Result<IReadOnlyList<StockView>>> ListProductsAsync(
            ListProductsQuery query,
            CancellationToken cancellationToken = default) =>
            Cancel<IReadOnlyList<StockView>>(cancellationToken);

        private async Task<Result<T>> Cancel<T>(CancellationToken cancellationToken)
        {
            await cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("La cancelación no llegó a la tool.");
        }
    }
}
