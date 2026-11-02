using Sandbox.Application.Sales;
using static Sandbox.Application.Tests.Sales.SalesHarness;

namespace Sandbox.Application.Tests.Sales;

public sealed class ListProductsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListProducts_OverSeedCatalog_ReturnsEveryProductOrderedBySku()
    {
        var harness = new SalesHarness();

        var result = await harness.Queries.ListProductsAsync(new ListProductsQuery(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [("SKU-CHAIR-ERGO", 10), ("SKU-LAMP-DESK", 0), ("SKU-MUG-RED", 20), ("SKU-TSHIRT-M", 5)],
            result.Value.Select(product => (product.Sku, product.Available)));
    }

    [Fact]
    public async Task ListProducts_WithAnOrderInProgress_ReflectsTheReservation()
    {
        var harness = new SalesHarness();
        await harness.PlacedOrderAsync("k1", "CUST-001", Line("SKU-MUG-RED", 3));

        var result = await harness.Queries.ListProductsAsync(new ListProductsQuery(), Token);

        Assert.Equal(17, Assert.Single(result.Value ?? [], product => product.Sku == "SKU-MUG-RED").Available);
    }

    [Fact]
    public async Task ListProducts_CalledSeveralTimes_DoesNotChangeTheState()
    {
        var harness = new SalesHarness();

        await harness.Queries.ListProductsAsync(new ListProductsQuery(), Token);
        await harness.Queries.ListProductsAsync(new ListProductsQuery(), Token);

        Assert.Equal("ORD-0001", (await harness.PlacedOrderAsync("k1", "CUST-001", Line("SKU-MUG-RED", 1))).OrderId);
        Assert.Equal(19, await harness.AvailableAsync("SKU-MUG-RED"));
    }
}
