using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;
using static Sandbox.Domain.Tests.Sales.SalesTestData;

namespace Sandbox.Domain.Tests.Sales;

public sealed class OrderTransitionTests
{
    private readonly StockItem _item = Product("SKU-A", onHand: 20);
    private readonly Dictionary<string, StockItem> _stock;
    private readonly Order _order;

    public OrderTransitionTests()
    {
        _stock = Stock(_item);
        _order = PlacedOrder(_stock, Line("SKU-A", 3));
    }

    [Fact]
    public void Ship_PlacedOrder_BecomesShippedAndConsumesStock()
    {
        var result = _order.Ship(_stock);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, _order.Status);
        Assert.Equal(17, _item.OnHand);
        Assert.Equal(0, _item.Reserved);
        Assert.Equal(17, _item.Available);
    }

    [Fact]
    public void Cancel_PlacedOrder_BecomesCancelledAndReleasesStock()
    {
        Assert.Equal(17, _item.Available);

        var result = _order.Cancel(_stock);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, _order.Status);
        Assert.Equal(20, _item.Available);
    }

    [Fact]
    public void Cancel_ShippedOrder_ReturnsInvalidStateTransitionAndStaysShipped()
    {
        _order.Ship(_stock);

        var result = _order.Cancel(_stock);

        AssertInvalidTransition(result);
        Assert.Equal(OrderStatus.Shipped, _order.Status);
        Assert.Equal(17, _item.OnHand);
    }

    [Fact]
    public void Cancel_CancelledOrder_ReturnsInvalidStateTransitionWithoutTouchingStock()
    {
        _order.Cancel(_stock);

        var result = _order.Cancel(_stock);

        AssertInvalidTransition(result);
        Assert.Equal(OrderStatus.Cancelled, _order.Status);
        Assert.Equal(20, _item.Available);
        Assert.Equal(0, _item.Reserved);
    }

    [Fact]
    public void Ship_CancelledOrder_ReturnsInvalidStateTransitionAndStaysCancelled()
    {
        _order.Cancel(_stock);

        var result = _order.Ship(_stock);

        AssertInvalidTransition(result);
        Assert.Equal(OrderStatus.Cancelled, _order.Status);
        Assert.Equal(20, _item.OnHand);
    }

    [Fact]
    public void Ship_ShippedOrder_ReturnsInvalidStateTransitionAndStaysShipped()
    {
        _order.Ship(_stock);

        var result = _order.Ship(_stock);

        AssertInvalidTransition(result);
        Assert.Equal(OrderStatus.Shipped, _order.Status);
        Assert.Equal(17, _item.OnHand);
    }

    private static void AssertInvalidTransition(Result<Order> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.Error.Code);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
    }
}
