using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using static Sandbox.Application.Tests.Sales.SalesHarness;

namespace Sandbox.Application.Tests.Sales;

public sealed class SalesQueriesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetStock_WithReservations_ReturnsNamePriceAndAvailable()
    {
        var harness = new SalesHarness(StateWith(Product("SKU-A", unitPrice: 12.50m, onHand: 20)));
        await harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 3));

        var result = await harness.Queries.GetStockAsync(new GetStockQuery("SKU-A"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(new StockView("SKU-A", "Producto SKU-A", 12.50m, 17), result.Value);
    }

    [Fact]
    public void StockView_Always_ExposesAvailableButNotOnHandOrReserved()
    {
        var properties = typeof(StockView).GetProperties().Select(property => property.Name);

        Assert.Equal(["Sku", "Name", "UnitPrice", "Available"], properties);
    }

    [Fact]
    public async Task GetOrder_ForUnknownOrder_ReturnsOrderNotFound()
    {
        var harness = new SalesHarness();

        var result = await harness.Queries.GetOrderAsync(new GetOrderQuery("ORD-9999"), Token);

        Assert.Equal(ErrorKind.NotFound, ResultAssert.Fails(result, ErrorCodes.OrderNotFound).Kind);
    }

    [Fact]
    public async Task GetCustomer_ForUnknownCustomer_ReturnsCustomerNotFound()
    {
        var harness = new SalesHarness();

        var result = await harness.Queries.GetCustomerAsync(new GetCustomerQuery("CUST-999"), Token);

        ResultAssert.Fails(result, ErrorCodes.CustomerNotFound);
    }

    [Fact]
    public async Task GetCustomer_ForBlockedSeedCustomer_ReturnsItsView()
    {
        var harness = new SalesHarness();

        var result = await harness.Queries.GetCustomerAsync(new GetCustomerQuery("CUST-003"), Token);

        Assert.Equal(new CustomerView("CUST-003", "Carla Demo", Domain.Sales.CustomerStatus.Blocked), result.Value);
    }

    [Fact]
    public async Task GetStock_ForUnknownProduct_ReturnsProductNotFoundNamingIt()
    {
        var harness = new SalesHarness();

        var result = await harness.Queries.GetStockAsync(new GetStockQuery("SKU-NOPE"), Token);

        Assert.Contains("SKU-NOPE", ResultAssert.Fails(result, ErrorCodes.ProductNotFound).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Queries_RunSeveralTimes_DoNotChangeOrdersStockOrNumbering()
    {
        var harness = new SalesHarness(StateWith(Product("SKU-A", onHand: 20)));
        var placed = await harness.PlacedOrderAsync("k1", ActiveCustomerId, Line("SKU-A", 3));

        for (var i = 0; i < 3; i++)
        {
            await harness.Queries.GetOrderAsync(new GetOrderQuery(placed.OrderId), Token);
            await harness.Queries.GetCustomerAsync(new GetCustomerQuery(ActiveCustomerId), Token);
            await harness.Queries.GetStockAsync(new GetStockQuery("SKU-A"), Token);
            await harness.Queries.GetOrderAsync(new GetOrderQuery("ORD-9999"), Token);
        }

        Assert.Equal(17, await harness.AvailableAsync("SKU-A"));
        Assert.Equal(Domain.Sales.OrderStatus.Placed, (await harness.OrderAsync(placed.OrderId)).Status);
        var next = await harness.PlacedOrderAsync("k2", ActiveCustomerId, Line("SKU-A", 1));
        Assert.Equal("ORD-0002", next.OrderId);
    }
}
