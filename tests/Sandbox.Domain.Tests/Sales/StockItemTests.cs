using Sandbox.Domain.Results;
using static Sandbox.Domain.Tests.Sales.SalesTestData;

namespace Sandbox.Domain.Tests.Sales;

public sealed class StockItemTests
{
    [Fact]
    public void Reserve_WithEnoughAvailable_LowersAvailable()
    {
        var item = Product("SKU-A", onHand: 20);

        var result = item.Reserve(3);

        Assert.True(result.IsSuccess);
        Assert.Equal(17, item.Available);
        Assert.Equal(3, item.Reserved);
        Assert.Equal(20, item.OnHand);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_ReturnsInsufficientStockNamingProductAndAvailable()
    {
        var item = Product("SKU-B", onHand: 2);

        var result = item.Reserve(3);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InsufficientStock, result.Error.Code);
        Assert.Equal(ErrorKind.BusinessRule, result.Error.Kind);
        Assert.Contains("SKU-B", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("2 unidades", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, item.Reserved);
    }

    [Fact]
    public void Release_AfterReserve_RestoresAvailable()
    {
        var item = Product("SKU-A", onHand: 20);
        item.Reserve(3);

        item.Release(3);

        Assert.Equal(20, item.Available);
        Assert.Equal(0, item.Reserved);
    }

    [Fact]
    public void Consume_AfterReserve_LowersOnHandAndReservedButNotAvailable()
    {
        var item = Product("SKU-A", onHand: 20);
        item.Reserve(3);

        item.Consume(3);

        Assert.Equal(17, item.OnHand);
        Assert.Equal(0, item.Reserved);
        Assert.Equal(17, item.Available);
    }
}
