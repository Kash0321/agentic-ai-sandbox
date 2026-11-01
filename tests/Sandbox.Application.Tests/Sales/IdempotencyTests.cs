using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;
using static Sandbox.Application.Tests.Sales.SalesHarness;

namespace Sandbox.Application.Tests.Sales;

public sealed class IdempotencyTests
{
    private readonly SalesHarness _harness = new(StateWith(Product("SKU-A", onHand: 20), Product("SKU-B", onHand: 20)));

    [Fact]
    public async Task PlaceOrder_RepeatedWithSameKeyAndArguments_ReturnsTheSameOrderWithoutNewEffects()
    {
        var first = await _harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-A", 2));

        var retry = await _harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-A", 2));

        Assert.True(retry.IsSuccess);
        Assert.Same(first.Value, retry.Value);
        Assert.Equal(18, await _harness.AvailableAsync("SKU-A"));
        Assert.False(await _harness.OrderExistsAsync("ORD-0002"));
    }

    [Fact]
    public async Task CancelOrder_RepeatedWithSameKey_ReturnsCancelledViewInsteadOfInvalidTransition()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2));
        await _harness.CancelAsync("k2", placed.OrderId);

        var retry = await _harness.CancelAsync("k2", placed.OrderId);

        Assert.Equal(OrderStatus.Cancelled, retry.Value?.Status);
        Assert.Equal(20, await _harness.AvailableAsync("SKU-A"));
    }

    [Fact]
    public async Task PlaceOrder_RepeatedAfterTheOrderChanged_ReturnsTheViewStoredTheFirstTime()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2));
        await _harness.CancelAsync("k2", placed.OrderId);

        var retry = await _harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-A", 2));

        Assert.Equal(OrderStatus.Placed, retry.Value?.Status);
        Assert.Equal(placed.OrderId, retry.Value?.OrderId);
        Assert.Equal(OrderStatus.Cancelled, (await _harness.OrderAsync(placed.OrderId)).Status);
    }

    [Fact]
    public async Task CancelOrder_WithAnotherKeyOnCancelledOrder_ReturnsInvalidStateTransition()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2));
        await _harness.CancelAsync("k2", placed.OrderId);

        var result = await _harness.CancelAsync("k3", placed.OrderId);

        ResultAssert.Fails(result, ErrorCodes.InvalidStateTransition);
    }

    [Fact]
    public async Task PlaceOrder_WithSameKeyAndOtherQuantity_ReturnsConflictWithoutCreatingOrders()
    {
        await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2));

        var result = await _harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-A", 3));

        Assert.Equal(ErrorKind.Conflict, ResultAssert.Fails(result, ErrorCodes.IdempotencyKeyConflict).Kind);
        Assert.False(await _harness.OrderExistsAsync("ORD-0002"));
        Assert.Equal(18, await _harness.AvailableAsync("SKU-A"));
    }

    [Fact]
    public async Task PlaceOrder_WithSameKeyAndLinesInAnotherOrder_ReturnsConflict()
    {
        await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2), Line("SKU-B", 1));

        var result = await _harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-B", 1), Line("SKU-A", 2));

        ResultAssert.Fails(result, ErrorCodes.IdempotencyKeyConflict);
        Assert.False(await _harness.OrderExistsAsync("ORD-0002"));
    }

    [Fact]
    public async Task CancelOrder_WithKeyUsedByPlaceOrder_ReturnsConflictAndLeavesTheOrder()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2));

        var result = await _harness.CancelAsync("k1", placed.OrderId);

        ResultAssert.Fails(result, ErrorCodes.IdempotencyKeyConflict);
        Assert.Equal(OrderStatus.Placed, (await _harness.OrderAsync(placed.OrderId)).Status);
    }

    [Fact]
    public async Task PlaceOrder_RetriedAfterInsufficientStockOnceStockIsFreed_CreatesTheOrder()
    {
        var other = await _harness.PlacedOrderAsync("k0", ActiveCustomerId, Line("SKU-A", 15));
        var failed = await _harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-A", 10));
        await _harness.CancelAsync("k2", other.OrderId);

        var retry = await _harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-A", 10));

        ResultAssert.Fails(failed, ErrorCodes.InsufficientStock);
        Assert.True(retry.IsSuccess);
        Assert.Equal(10, await _harness.AvailableAsync("SKU-A"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("clave\u0007con-control")]
    public async Task Commands_WithMalformedKey_ReturnInvalidIdempotencyKey(string key)
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2));

        var place = await _harness.PlaceAsync(key, ActiveCustomerId, Line("SKU-A", 1));
        var cancel = await _harness.CancelAsync(key, placed.OrderId);
        var ship = await _harness.ShipAsync(key, placed.OrderId);

        Assert.Equal(ErrorKind.Validation, ResultAssert.Fails(place, ErrorCodes.InvalidIdempotencyKey).Kind);
        ResultAssert.Fails(cancel, ErrorCodes.InvalidIdempotencyKey);
        ResultAssert.Fails(ship, ErrorCodes.InvalidIdempotencyKey);
        Assert.Equal(18, await _harness.AvailableAsync("SKU-A"));
    }

    [Fact]
    public async Task ShipOrder_WithKeyOf65Characters_ReturnsInvalidIdempotencyKeyAndLeavesTheOrder()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2));

        var result = await _harness.ShipAsync(new string('k', 65), placed.OrderId);

        ResultAssert.Fails(result, ErrorCodes.InvalidIdempotencyKey);
        Assert.Equal(OrderStatus.Placed, (await _harness.OrderAsync(placed.OrderId)).Status);
    }

    [Fact]
    public async Task ShipOrder_WithKeyOf64Characters_Succeeds()
    {
        var placed = await _harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 2));

        var result = await _harness.ShipAsync(new string('k', 64), placed.OrderId);

        Assert.True(result.IsSuccess);
    }
}
