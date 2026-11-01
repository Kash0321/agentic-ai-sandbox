using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;
using static Sandbox.Application.Tests.Sales.SalesHarness;

namespace Sandbox.Application.Tests.Sales;

public sealed class PlaceOrderTests
{
    [Fact]
    public async Task PlaceOrder_ForActiveCustomerWithStock_ReturnsPlacedOrderWithLineTotalAndClockDate()
    {
        var harness = new SalesHarness();

        var result = await harness.PlaceAsync("k1", "CUST-001", Line("SKU-MUG-RED", 2));

        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.Equal("ORD-0001", order.OrderId);
        Assert.Equal("CUST-001", order.CustomerId);
        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(new OrderLineView("SKU-MUG-RED", 2, 12.50m, 25.00m), Assert.Single(order.Lines));
        Assert.Equal(25.00m, order.Total);
        Assert.Equal("EUR", order.Currency);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero), order.PlacedAt);
        Assert.Equal(18, await harness.AvailableAsync("SKU-MUG-RED"));
    }

    [Fact]
    public async Task PlaceOrder_AfterTheClockAdvances_UsesTheNewDate()
    {
        var harness = new SalesHarness();
        harness.Clock.Advance(TimeSpan.FromHours(2));

        var order = await harness.PlacedOrderAsync("k1", "CUST-001", Line("SKU-MUG-RED", 1));

        Assert.Equal(Now.AddHours(2), order.PlacedAt);
    }

    [Fact]
    public async Task PlaceOrder_ForUnknownCustomer_ReturnsCustomerNotFoundWithoutCreatingOrders()
    {
        var harness = new SalesHarness();

        var result = await harness.PlaceAsync("k1", "CUST-999", Line("SKU-MUG-RED", 1));

        Assert.Equal(ErrorKind.NotFound, ResultAssert.Fails(result, ErrorCodes.CustomerNotFound).Kind);
        Assert.False(await harness.OrderExistsAsync("ORD-0001"));
    }

    [Fact]
    public async Task PlaceOrder_WithUnknownProduct_ReturnsProductNotFoundNamingItWithoutReserving()
    {
        var harness = new SalesHarness();

        var result = await harness.PlaceAsync("k1", "CUST-001", Line("SKU-MUG-RED", 1), Line("SKU-NOPE", 1));

        var error = ResultAssert.Fails(result, ErrorCodes.ProductNotFound);
        Assert.Contains("SKU-NOPE", error.Message, StringComparison.Ordinal);
        Assert.Equal(20, await harness.AvailableAsync("SKU-MUG-RED"));
    }

    [Fact]
    public async Task PlaceOrder_ForBlockedCustomerLackingStock_ReturnsCustomerBlocked()
    {
        var harness = new SalesHarness();

        var result = await harness.PlaceAsync("k1", "CUST-003", Line("SKU-TSHIRT-M", 6));

        ResultAssert.Fails(result, ErrorCodes.CustomerBlocked);
    }

    [Fact]
    public async Task PlaceOrder_ForUnknownCustomerWithZeroQuantity_ReturnsInvalidQuantity()
    {
        var harness = new SalesHarness();

        var result = await harness.PlaceAsync("k1", "CUST-999", Line("SKU-MUG-RED", 0));

        ResultAssert.Fails(result, ErrorCodes.InvalidQuantity);
    }

    [Fact]
    public async Task PlaceOrder_WhenOneLineLacksStock_ReturnsInsufficientStockWithoutPartialReservations()
    {
        var harness = new SalesHarness(StateWith(Product("SKU-A", onHand: 10), Product("SKU-B", onHand: 2)));

        var result = await harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-A", 5), Line("SKU-B", 3));

        var error = ResultAssert.Fails(result, ErrorCodes.InsufficientStock);
        Assert.Contains("SKU-B", error.Message, StringComparison.Ordinal);
        Assert.Equal(10, await harness.AvailableAsync("SKU-A"));
        Assert.Equal(2, await harness.AvailableAsync("SKU-B"));
    }

    [Fact]
    public async Task PlaceOrder_AfterAFailedAttempt_DoesNotSkipANumber()
    {
        var harness = new SalesHarness(StateWith(Product("SKU-A", onHand: 3)));

        var first = await harness.PlaceAsync("k1", ActiveCustomerId, Line("SKU-A", 1));
        var failed = await harness.PlaceAsync("k2", ActiveCustomerId, Line("SKU-A", 5));
        var third = await harness.PlaceAsync("k3", ActiveCustomerId, Line("SKU-A", 1));

        ResultAssert.Fails(failed, ErrorCodes.InsufficientStock);
        Assert.Equal("ORD-0001", first.Value?.OrderId);
        Assert.Equal("ORD-0002", third.Value?.OrderId);
    }

    [Fact]
    public async Task PlaceOrder_WithSeedChair_AcceptsFourUnitsAndRejectsFive()
    {
        var harness = new SalesHarness();

        var five = await harness.PlaceAsync("k1", "CUST-001", Line("SKU-CHAIR-ERGO", 5));
        var four = await harness.PlaceAsync("k2", "CUST-001", Line("SKU-CHAIR-ERGO", 4));

        ResultAssert.Fails(five, ErrorCodes.OrderLimitExceeded);
        Assert.Equal(996.00m, four.Value?.Total);
    }
}
