using Sandbox.Agents.Tools;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;
using static Sandbox.Agents.Tests.Tools.ToolHarness;

namespace Sandbox.Agents.Tests.Tools;

/// <summary>
/// Las tools actúan en nombre del cliente de sesión: un pedido de otro cliente se responde como inexistente (spec
/// <c>agent-tools</c>, design D1 y D2 de <c>add-agent-guardrails</c>).
/// </summary>
public sealed class SalesToolsAuthorizationTests
{
    private const string Owner = "CUST-001";

    [Fact]
    public async Task GetOrder_OfAnotherCustomer_ReturnsOrderNotFoundWithoutOrderData()
    {
        using var harness = new ToolHarness("CUST-002");
        await PlaceOrderOfOwnerAsync(harness);
        var missing = await harness.InvokeAsync(SalesToolNames.GetOrder, new { orderId = "ORD-9999" });

        var result = await harness.InvokeAsync(SalesToolNames.GetOrder, new { orderId = "ORD-0001" });

        Assert.Equal(ErrorCodes.OrderNotFound, ErrorCode(result));
        Assert.Equal(Message(missing), Message(result));
        Assert.False(result.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task CancelOrder_OfAnotherCustomer_ReturnsOrderNotFoundAndKeepsTheOrderPlaced()
    {
        using var harness = new ToolHarness("CUST-002");
        await PlaceOrderOfOwnerAsync(harness);

        var result = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "k1" });

        Assert.Equal(ErrorCodes.OrderNotFound, ErrorCode(result));
        var order = await harness.Queries.GetOrderAsync(new GetOrderQuery("ORD-0001", Owner), TestContext.Current.CancellationToken);
        Assert.Equal(OrderStatus.Placed, order.Value?.Status);
    }

    [Fact]
    public async Task CancelOrder_OfTheSessionCustomer_CancelsIt()
    {
        using var harness = new ToolHarness(Owner);
        await PlaceOrderOfOwnerAsync(harness);

        var result = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "k1" });

        Assert.Equal("Cancelled", result.GetProperty("data").GetProperty("status").GetString());
    }

    private static async Task PlaceOrderOfOwnerAsync(ToolHarness harness)
    {
        var placed = await harness.Commands.PlaceOrderAsync(
            new PlaceOrderCommand("k0", Owner, [new OrderLineRequest("SKU-MUG-RED", 2)]),
            TestContext.Current.CancellationToken);
        Assert.Equal("ORD-0001", placed.Value?.OrderId);
    }

    private static string? Message(System.Text.Json.JsonElement result) =>
        result.GetProperty("error").GetProperty("message").GetString();
}
