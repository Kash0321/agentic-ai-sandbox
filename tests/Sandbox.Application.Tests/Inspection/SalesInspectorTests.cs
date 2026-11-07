using Sandbox.Application.Inspection;
using Sandbox.Application.Tests.Sales;
using Sandbox.Domain.Sales;

namespace Sandbox.Application.Tests.Inspection;

/// <summary>Spec <c>order-operations</c>, "Lectura de inspección".</summary>
public sealed class SalesInspectorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SnapshotAsync_WithOrdersOfTwoCustomers_ContainsBothOrdersAndTheStockLevels()
    {
        var harness = new SalesHarness();
        await harness.PlacedOrderAsync("k-1", "CUST-001", SalesHarness.Line("SKU-MUG-RED", 2));
        await harness.PlacedOrderAsync("k-2", "CUST-002", SalesHarness.Line("SKU-MUG-RED", 1));
        Assert.True((await harness.CancelAsync("k-3", "ORD-0002")).IsSuccess);

        var snapshot = await new SalesInspector(harness.State).SnapshotAsync(Token);

        Assert.Collection(
            snapshot.Orders,
            order =>
            {
                Assert.Equal(("ORD-0001", "CUST-001", OrderStatus.Placed), (order.OrderId, order.CustomerId, order.Status));
                Assert.Equal(("SKU-MUG-RED", 2), (Assert.Single(order.Lines).Sku, order.Lines[0].Quantity));
            },
            order => Assert.Equal(("ORD-0002", "CUST-002", OrderStatus.Cancelled), (order.OrderId, order.CustomerId, order.Status)));
        Assert.Equal(new StockLevel("SKU-MUG-RED", 20, 2, 18), Assert.Single(snapshot.Stock, item => item.Sku == "SKU-MUG-RED"));
        Assert.Equal(
            ["SKU-CHAIR-ERGO", "SKU-LAMP-DESK", "SKU-MUG-RED", "SKU-TSHIRT-M"],
            snapshot.Stock.Select(item => item.Sku));
    }

    [Fact]
    public async Task SnapshotAsync_RepeatedSeveralTimes_ChangesNothing()
    {
        var harness = new SalesHarness();
        await harness.PlacedOrderAsync("k-1", "CUST-001", SalesHarness.Line("SKU-MUG-RED", 2));
        var inspector = new SalesInspector(harness.State);

        var first = await inspector.SnapshotAsync(Token);
        var second = await inspector.SnapshotAsync(Token);
        var next = await harness.PlacedOrderAsync("k-2", "CUST-002", SalesHarness.Line("SKU-MUG-RED", 1));

        Assert.Equal(first.Stock, second.Stock);
        Assert.Equal(first.Orders.Select(order => (order.OrderId, order.Status)), second.Orders.Select(order => (order.OrderId, order.Status)));
        Assert.Equal("ORD-0002", next.OrderId);
    }

    [Fact]
    public async Task SnapshotAsync_WithACanceledToken_Throws()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => new SalesInspector(new SalesHarness().State).SnapshotAsync(source.Token));
    }
}
