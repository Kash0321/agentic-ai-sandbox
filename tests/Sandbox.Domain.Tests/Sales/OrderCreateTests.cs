using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;
using static Sandbox.Domain.Tests.Sales.SalesTestData;

namespace Sandbox.Domain.Tests.Sales;

public sealed class OrderCreateTests
{
    [Fact]
    public void Create_WithActiveCustomerAndValidLines_IsPlacedWithCustomerAndDate()
    {
        var customer = ActiveCustomer();

        var result = Create(customer, Stock(Product("SKU-A")), Line("SKU-A", 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Placed, result.Value.Status);
        Assert.Equal(customer.Id, result.Value.CustomerId);
        Assert.Equal(PlacedAt, result.Value.PlacedAt);
        Assert.Equal(OrderId, result.Value.Id);
    }

    [Fact]
    public void Create_WithoutLines_ReturnsEmptyOrder()
    {
        var result = Create(ActiveCustomer(), Stock(Product("SKU-A")));

        AssertError(result, ErrorCodes.EmptyOrder, ErrorKind.Validation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_WithQuantityOutOfRange_ReturnsInvalidQuantity(int quantity)
    {
        var result = Create(ActiveCustomer(), Stock(Product("SKU-A", onHand: 200)), Line("SKU-A", quantity));

        AssertError(result, ErrorCodes.InvalidQuantity, ErrorKind.Validation);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Create_WithQuantityAtTheLimits_Succeeds(int quantity)
    {
        var result = Create(ActiveCustomer(), Stock(Product("SKU-A", unitPrice: 10.00m, onHand: 100)), Line("SKU-A", quantity));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_WithRepeatedProduct_ReturnsDuplicateOrderLine()
    {
        var result = Create(ActiveCustomer(), Stock(Product("SKU-A")), Line("SKU-A", 1), Line("SKU-A", 2));

        AssertError(result, ErrorCodes.DuplicateOrderLine, ErrorKind.Validation);
    }

    [Fact]
    public void Create_ForBlockedCustomer_ReturnsCustomerBlockedWithoutReserving()
    {
        var item = Product("SKU-A", onHand: 20);

        var result = Create(BlockedCustomer(), Stock(item), Line("SKU-A", 2));

        AssertError(result, ErrorCodes.CustomerBlocked, ErrorKind.BusinessRule);
        Assert.Equal(20, item.Available);
    }

    [Fact]
    public void Create_WithUnknownProduct_ReturnsProductNotFoundNamingIt()
    {
        var result = Create(ActiveCustomer(), Stock(Product("SKU-A")), Line("SKU-NOPE", 1));

        AssertError(result, ErrorCodes.ProductNotFound, ErrorKind.NotFound);
        Assert.Contains("SKU-NOPE", result.Error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_AboveTheTotalLimit_ReturnsOrderLimitExceededWithoutReserving()
    {
        var cheap = Product("SKU-CHEAP", unitPrice: 0.01m);
        var expensive = Product("SKU-EXPENSIVE", unitPrice: 250.00m);

        var result = Create(ActiveCustomer(), Stock(cheap, expensive), Line("SKU-EXPENSIVE", 4), Line("SKU-CHEAP", 1));

        AssertError(result, ErrorCodes.OrderLimitExceeded, ErrorKind.BusinessRule);
        Assert.Equal(0, cheap.Reserved);
        Assert.Equal(0, expensive.Reserved);
    }

    [Fact]
    public void Create_ExactlyAtTheTotalLimit_SucceedsWithThatTotal()
    {
        var result = Create(ActiveCustomer(), Stock(Product("SKU-A", unitPrice: 250.00m)), Line("SKU-A", 4));

        Assert.True(result.IsSuccess);
        Assert.Equal(1000.00m, result.Value.Total);
        Assert.Equal("EUR", result.Value.Currency);
    }

    [Fact]
    public void Create_WithCatalogPrice_KeepsUnitPriceAndAmountInTheLine()
    {
        var result = Create(ActiveCustomer(), Stock(Product("SKU-MUG", unitPrice: 12.50m)), Line("SKU-MUG", 2));

        var line = Assert.Single(result.Value?.Lines ?? []);
        Assert.Equal(12.50m, line.UnitPrice);
        Assert.Equal(25.00m, line.Amount);
        Assert.Equal(25.00m, result.Value?.Total);
    }

    [Fact]
    public void Create_WithEnoughStock_ReservesEachLine()
    {
        var item = Product("SKU-A", onHand: 20);

        Create(ActiveCustomer(), Stock(item), Line("SKU-A", 3));

        Assert.Equal(17, item.Available);
    }

    [Fact]
    public void Create_WhenOneLineLacksStock_ReturnsInsufficientStockAndReservesNothing()
    {
        var productA = Product("SKU-A", onHand: 10);
        var productB = Product("SKU-B", onHand: 2);

        var result = Create(ActiveCustomer(), Stock(productA, productB), Line("SKU-A", 5), Line("SKU-B", 3));

        AssertError(result, ErrorCodes.InsufficientStock, ErrorKind.BusinessRule);
        Assert.Contains("SKU-B", result.Error?.Message, StringComparison.Ordinal);
        Assert.Contains("2 unidades", result.Error?.Message, StringComparison.Ordinal);
        Assert.Equal(10, productA.Available);
    }

    [Fact]
    public void Create_ForProductWithoutStock_ReturnsInsufficientStock()
    {
        var result = Create(ActiveCustomer(), Stock(Product("SKU-LAMP", onHand: 0)), Line("SKU-LAMP", 1));

        AssertError(result, ErrorCodes.InsufficientStock, ErrorKind.BusinessRule);
    }

    [Fact]
    public void Create_InTwoIndependentRunsLackingStock_ReturnsTheSameCodeAndKind()
    {
        var first = Create(ActiveCustomer(), Stock(Product("SKU-A", onHand: 1)), Line("SKU-A", 2));
        var second = Create(ActiveCustomer(), Stock(Product("SKU-A", onHand: 1)), Line("SKU-A", 2));

        Assert.Equal(first.Error, second.Error);
    }

    [Fact]
    public void Create_WithEveryBrokenRule_NeverThrows()
    {
        var stock = Stock(Product("SKU-A", unitPrice: 600.00m, onHand: 1));
        OrderLineRequest[][] brokenRequests =
        [
            [],
            [Line("SKU-A", 0)],
            [Line("SKU-A", 1), Line("SKU-A", 1)],
            [Line("SKU-NOPE", 1)],
            [Line("SKU-A", 2)],
        ];

        foreach (var lines in brokenRequests)
        {
            var exception = Record.Exception(() => Create(ActiveCustomer(), stock, lines));

            Assert.Null(exception);
        }

        Assert.Null(Record.Exception(() => Create(BlockedCustomer(), stock, Line("SKU-A", 1))));
    }

    private static void AssertError(Result<Order> result, string code, ErrorKind kind)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(code, result.Error.Code);
        Assert.Equal(kind, result.Error.Kind);
    }
}
