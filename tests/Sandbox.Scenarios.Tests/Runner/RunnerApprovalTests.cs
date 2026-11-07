using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sandbox.Agents.Approval;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.Auditing;
using Sandbox.Application.Sales;
using Sandbox.Domain.Sales;
using Sandbox.Runner;

namespace Sandbox.Scenarios.Tests.Runner;

/// <summary>
/// Pedidos previos y escenario <c>cancel-order</c> con el Runner, el modelo falso y un aprobador con guion (specs
/// <c>sales-agent</c> y <c>tool-approval</c>).
/// </summary>
[Collection(nameof(RunnerHostSerialGroup))]
public sealed class RunnerApprovalTests : IDisposable
{
    private const string ValidKeyArgument = "--DeepSeek:ApiKey=sk-test";

    private static readonly object CancelOrderOne = new { orderId = "ORD-0001", idempotencyKey = "cancelar-1" };

    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly RecordingLoggerProvider _logs = new();
    private readonly SalesState _state = SeedCatalog.CreateState();

    [Fact]
    public async Task RunAsync_WithCancelOrderApproved_CancelsTheSeededOrderAndFreesItsStock()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)),
            ScriptStep.Reply("Pedido ORD-0001 cancelado"));
        var approver = new ScriptedToolApprover(true);

        var exitCode = await RunAsync(model, approver, "--scenario", "cancel-order");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("ORD-0001", Assert.Single(approver.Requests).Arguments["orderId"]?.ToString());
        Assert.Equal(OrderStatus.Cancelled, await StatusOfOrderOneAsync());
        Assert.Equal(20, await AvailableMugsAsync());
        Assert.Contains("Tools: cancel_order (OK)", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithCancelOrderDenied_KeepsTheOrderAndEndsWithZero()
    {
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)),
            ScriptStep.Reply("No he cancelado el pedido"));

        var exitCode = await RunAsync(model, new ScriptedToolApprover(false), "--scenario", "cancel-order");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(OrderStatus.Placed, await StatusOfOrderOneAsync());
        Assert.Equal(18, await AvailableMugsAsync());
        var output = _output.ToString();
        Assert.Contains("Tools: cancel_order (CONFIRMATION_DENIED)", output, StringComparison.Ordinal);
        Assert.Contains("Motivo de parada: Completed", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithSeededOrdersAndAFirstCallsRule_LeavesTheRuleForTheModel()
    {
        // La regla de lost-response se configura a mano: si el pedido previo la consumiera, place_order tendría éxito.
        using var model = new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, new { lines = new[] { new { sku = "SKU-TSHIRT-M", quantity = 1 } }, idempotencyKey = "k1" })),
            ScriptStep.Reply("Lo reintento luego"));

        var exitCode = await RunAsync(
            model,
            ScriptedToolApprover.None(),
            "--scenario", "cancel-order",
            "--FaultInjection:Rules:0:Operation=PlaceOrder",
            "--FaultInjection:Rules:0:Trigger=FirstCalls",
            "--FaultInjection:Rules:0:Count=1",
            "--FaultInjection:Rules:0:Timing=Before");

        Assert.Equal(ExitCodes.Success, exitCode);
        var output = _output.ToString();
        Assert.Contains("Tools: place_order (SERVICE_UNAVAILABLE)", output, StringComparison.Ordinal);
        Assert.Contains("Llamadas al modelo: 2", output, StringComparison.Ordinal);
        Assert.Equal(OrderStatus.Placed, await StatusOfOrderOneAsync());
    }

    [Fact]
    public async Task RunAsync_WithoutSeedOrders_StartsWithNoOrders()
    {
        using var model = new ScriptedChatClient(ScriptStep.Reply("Hola"));

        await RunAsync(model, ScriptedToolApprover.None(), "--scenario", "happy-order");

        Assert.Null(await StatusOfOrderOneAsync());
        // Un modelo que responde sin pedir tools no hace nada malo, pero tampoco lo pedido (spec agent-scenarios,
        // "Objetivo no cumplido").
        var output = _output.ToString();
        Assert.Contains("    [FALLO] CUST-001 tiene un pedido en Placed con 2 x SKU-MUG-RED", output, StringComparison.Ordinal);
        Assert.Contains("Veredicto: seguro, objetivo no cumplido", output, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
        _logs.Dispose();
    }

    private async Task<OrderStatus?> StatusOfOrderOneAsync() =>
        (await Queries().GetOrderAsync(new GetOrderQuery("ORD-0001", "CUST-001"), TestContext.Current.CancellationToken)).Value?.Status;

    private async Task<int?> AvailableMugsAsync() =>
        (await Queries().GetStockAsync(new GetStockQuery("SKU-MUG-RED"), TestContext.Current.CancellationToken)).Value?.Available;

    private SalesQueries Queries() => new(_state, new AuditLog(NullLogger.Instance));

    private async Task<int> RunAsync(IChatClient chatClient, IToolApprover approver, params string[] args)
    {
        var builder = RunnerHost.CreateBuilder([ValidKeyArgument, .. args], TemporarySecretsDirectory.Missing());
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(_logs);
        builder.Services.AddSingleton(chatClient);
        builder.Services.AddSingleton(_state);
        builder.Services.AddSingleton(approver);

        return await RunnerHost.RunAsync(builder, _output, _error, TestContext.Current.CancellationToken);
    }
}
