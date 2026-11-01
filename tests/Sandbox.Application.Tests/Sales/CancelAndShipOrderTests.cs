using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;
using static Sandbox.Application.Tests.Sales.SalesHarness;

namespace Sandbox.Application.Tests.Sales;

public sealed class CancelAndShipOrderTests
{
    private readonly SalesHarness _harness = new(StateWith(Product("SKU-A", onHand: 20)));

    [Fact]
    public async Task CancelOrder_PlacedOrder_ReturnsCancelledViewAndReleasesStock()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 3));

        var result = await _harness.CancelAsync("k2", placed.OrderId);

        Assert.Equal(OrderStatus.Cancelled, result.Value?.Status);
        Assert.Equal(20, await _harness.AvailableAsync("SKU-A"));
    }

    [Fact]
    public async Task ShipOrder_PlacedOrder_ReturnsShippedViewAndKeepsAvailable()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 3));

        var result = await _harness.ShipAsync("k2", placed.OrderId);

        Assert.Equal(OrderStatus.Shipped, result.Value?.Status);
        Assert.Equal(17, await _harness.AvailableAsync("SKU-A"));
    }

    [Fact]
    public async Task CancelOrder_ForUnknownOrder_ReturnsOrderNotFound()
    {
        var result = await _harness.CancelAsync("k1", "ORD-9999");

        ResultAssert.Fails(result, ErrorCodes.OrderNotFound);
    }

    [Fact]
    public async Task ShipOrder_ForUnknownOrder_ReturnsOrderNotFound()
    {
        var result = await _harness.ShipAsync("k1", "ORD-9999");

        ResultAssert.Fails(result, ErrorCodes.OrderNotFound);
    }

    [Fact]
    public async Task CancelOrder_ShippedOrder_ReturnsInvalidStateTransitionWithoutTouchingStock()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 3));
        await _harness.ShipAsync("k2", placed.OrderId);

        var result = await _harness.CancelAsync("k3", placed.OrderId);

        Assert.Equal(ErrorKind.Conflict, ResultAssert.Fails(result, ErrorCodes.InvalidStateTransition).Kind);
        Assert.Equal(OrderStatus.Shipped, (await _harness.OrderAsync(placed.OrderId)).Status);
        Assert.Equal(17, await _harness.AvailableAsync("SKU-A"));
    }

    [Fact]
    public async Task ShipOrder_CancelledOrder_ReturnsInvalidStateTransitionWithoutTouchingStock()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 3));
        await _harness.CancelAsync("k2", placed.OrderId);

        var result = await _harness.ShipAsync("k3", placed.OrderId);

        ResultAssert.Fails(result, ErrorCodes.InvalidStateTransition);
        Assert.Equal(OrderStatus.Cancelled, (await _harness.OrderAsync(placed.OrderId)).Status);
        Assert.Equal(20, await _harness.AvailableAsync("SKU-A"));
    }

    [Fact]
    public async Task ShipOrder_ShippedOrder_ReturnsInvalidStateTransition()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 3));
        await _harness.ShipAsync("k2", placed.OrderId);

        var result = await _harness.ShipAsync("k3", placed.OrderId);

        ResultAssert.Fails(result, ErrorCodes.InvalidStateTransition);
        Assert.Equal(17, await _harness.AvailableAsync("SKU-A"));
    }
}
