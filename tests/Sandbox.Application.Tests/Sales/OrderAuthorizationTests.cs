using Microsoft.Extensions.Logging;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;
using static Sandbox.Application.Tests.Sales.SalesHarness;

namespace Sandbox.Application.Tests.Sales;

/// <summary>
/// Autorización por recurso, idempotencia por ámbito y auditoría de denegaciones (design D1 a D4 de
/// <c>add-agent-guardrails</c>), sobre el catálogo semilla, que tiene dos clientes activos.
/// </summary>
public sealed class OrderAuthorizationTests
{
    private const string Owner = "CUST-001";
    private const string Other = "CUST-002";

    private readonly SalesHarness _harness = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CancelOrder_OfAnotherCustomer_ReturnsOrderNotFoundAndChangesNothing()
    {
        var placed = await _harness.PlacedOrderAsync("k1", Owner, Line("SKU-MUG-RED", 2));
        var missing = await _harness.CancelAsync("k2", "ORD-9999", Other);

        var result = await _harness.CancelAsync("k2", placed.OrderId, Other);

        var error = ResultAssert.Fails(result, ErrorCodes.OrderNotFound);
        Assert.Equal(ErrorKind.NotFound, error.Kind);
        Assert.Equal(ResultAssert.Fails(missing, ErrorCodes.OrderNotFound).Message, error.Message);
        Assert.Equal(OrderStatus.Placed, (await _harness.OrderAsync(placed.OrderId)).Status);
        Assert.Equal(18, await _harness.AvailableAsync("SKU-MUG-RED"));
    }

    [Fact]
    public async Task GetOrder_OfAnotherCustomer_ReturnsOrderNotFoundWithTheMessageOfAMissingOrder()
    {
        var placed = await _harness.PlacedOrderAsync("k1", Owner, Line("SKU-MUG-RED", 2));
        var missing = await _harness.Queries.GetOrderAsync(new GetOrderQuery("ORD-9999", Other), Token);

        var result = await _harness.Queries.GetOrderAsync(new GetOrderQuery(placed.OrderId, Other), Token);

        var error = ResultAssert.Fails(result, ErrorCodes.OrderNotFound);
        Assert.Equal(ResultAssert.Fails(missing, ErrorCodes.OrderNotFound).Message, error.Message);
    }

    [Fact]
    public async Task GetOrder_OfItsOwnCustomer_ReturnsTheOrder()
    {
        var placed = await _harness.PlacedOrderAsync("k1", Owner, Line("SKU-MUG-RED", 2));

        var result = await _harness.Queries.GetOrderAsync(new GetOrderQuery(placed.OrderId, Owner), Token);

        Assert.Equal(placed.OrderId, result.Value?.OrderId);
    }

    [Fact]
    public async Task CancelOrder_RepeatedByAnotherCustomerWithTheSameKey_ReturnsOrderNotFoundInsteadOfTheStoredView()
    {
        var placed = await _harness.PlacedOrderAsync("k0", Owner, Line("SKU-MUG-RED", 2));
        var cancelled = await _harness.CancelAsync("k1", placed.OrderId, Owner);

        var replayByOther = await _harness.CancelAsync("k1", placed.OrderId, Other);

        Assert.True(cancelled.IsSuccess);
        ResultAssert.Fails(replayByOther, ErrorCodes.OrderNotFound);
    }

    [Fact]
    public async Task PlaceOrder_WithAKeyUsedByAnotherCustomer_CreatesAnIndependentOrder()
    {
        await _harness.PlacedOrderAsync("k1", Owner, Line("SKU-MUG-RED", 2));

        var result = await _harness.PlaceAsync("k1", Other, Line("SKU-TSHIRT-M", 1));

        Assert.Equal("ORD-0002", result.Value?.OrderId);
        Assert.Equal(Other, result.Value?.CustomerId);
    }

    [Fact]
    public async Task ShipOrder_WithAKeyUsedByPlaceOrder_IsInTheWarehouseScopeAndSucceeds()
    {
        var placed = await _harness.PlacedOrderAsync("k1", Owner, Line("SKU-MUG-RED", 2));

        var result = await _harness.ShipAsync("k1", placed.OrderId);

        Assert.Equal(OrderStatus.Shipped, result.Value?.Status);
    }

    [Fact]
    public async Task GetOrder_OfAnotherCustomer_WritesAnAccessDeniedAuditEvent()
    {
        var placed = await _harness.PlacedOrderAsync("k1", Owner, Line("SKU-MUG-RED", 2));

        await _harness.Queries.GetOrderAsync(new GetOrderQuery(placed.OrderId, Other), Token);

        var denied = Assert.Single(_harness.AuditLogger.Entries);
        Assert.Equal("AccessDenied", denied.EventId.Name);
        Assert.Equal(LogLevel.Warning, denied.Level);
        Assert.Equal("GetOrder", denied.Properties["Operation"]);
        Assert.Equal(placed.OrderId, denied.Properties["OrderId"]);
        Assert.Equal(Other, denied.Properties["CustomerId"]);
        Assert.Equal("pedido de otro cliente", denied.Properties["Reason"]);
    }

    [Fact]
    public async Task CancelOrder_OfAnotherCustomer_WritesAnAccessDeniedAuditEvent()
    {
        var placed = await _harness.PlacedOrderAsync("k1", Owner, Line("SKU-MUG-RED", 2));

        await _harness.CancelAsync("k2", placed.OrderId, Other);

        var denied = Assert.Single(_harness.AuditLogger.Entries);
        Assert.Equal("CancelOrder", denied.Properties["Operation"]);
        Assert.Equal(Other, denied.Properties["CustomerId"]);
    }

    [Fact]
    public async Task GetOrderAndCancelOrder_OfAMissingOrder_WriteNoAccessDeniedEvent()
    {
        await _harness.Queries.GetOrderAsync(new GetOrderQuery("ORD-9999", Other), Token);
        await _harness.CancelAsync("k1", "ORD-9999", Other);

        Assert.Empty(_harness.AuditLogger.Entries);
    }

    [Fact]
    public async Task OrderOperations_OfTheOwner_WriteNoAccessDeniedEvent()
    {
        var placed = await _harness.PlacedOrderAsync("k1", Owner, Line("SKU-MUG-RED", 2));

        await _harness.Queries.GetOrderAsync(new GetOrderQuery(placed.OrderId, Owner), Token);
        await _harness.CancelAsync("k2", placed.OrderId, Owner);

        Assert.Empty(_harness.AuditLogger.Entries);
    }
}
