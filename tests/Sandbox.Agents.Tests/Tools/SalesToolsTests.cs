using Sandbox.Agents.Tools;
using Sandbox.Application.FaultInjection;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using static Sandbox.Agents.Tests.Tools.ToolHarness;

namespace Sandbox.Agents.Tests.Tools;

public sealed class SalesToolsTests
{
    private static readonly object TwoMugs = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } };

    [Fact]
    public async Task ListProducts_OverSeedCatalog_ReturnsTheFourProductsWithTheirAvailable()
    {
        using var harness = new ToolHarness();

        var result = await harness.InvokeAsync(SalesToolNames.ListProducts);

        Assert.True(result.GetProperty("success").GetBoolean());
        var products = result.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(4, products.Count);
        Assert.Contains(products, product => product.GetProperty("available").GetInt32() == 0);
    }

    [Fact]
    public async Task GetStock_ForExistingProduct_ReturnsAvailableAndPrice()
    {
        using var harness = new ToolHarness();

        var data = (await harness.InvokeAsync(SalesToolNames.GetStock, new { sku = "SKU-MUG-RED" })).GetProperty("data");

        Assert.Equal(20, data.GetProperty("available").GetInt32());
        Assert.Equal(12.50m, data.GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    public async Task GetCustomer_ForBlockedSessionCustomer_ReturnsItsNameAndBlockedStatus()
    {
        using var harness = new ToolHarness("CUST-003");

        var data = (await harness.InvokeAsync(SalesToolNames.GetCustomer)).GetProperty("data");

        Assert.Equal("Carla Demo", data.GetProperty("name").GetString());
        Assert.Equal("Blocked", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetOrder_ForExistingOrder_ReturnsItsView()
    {
        using var harness = new ToolHarness();
        await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k1" });

        var data = (await harness.InvokeAsync(SalesToolNames.GetOrder, new { orderId = "ORD-0001" })).GetProperty("data");

        Assert.Equal("Placed", data.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PlaceOrder_ForSessionCustomer_CreatesTheOrderInItsName()
    {
        using var harness = new ToolHarness("CUST-002");

        var result = await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k1" });

        var data = result.GetProperty("data");
        Assert.Equal("CUST-002", data.GetProperty("customerId").GetString());
        Assert.Equal("Placed", data.GetProperty("status").GetString());
        Assert.Equal(25.00m, data.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task PlaceOrder_RepeatedWithTheSameKey_ReturnsTheSameOrderWithoutCreatingAnother()
    {
        using var harness = new ToolHarness();
        await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k1" });

        var retry = await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k1" });

        Assert.Equal("ORD-0001", retry.GetProperty("data").GetProperty("orderId").GetString());
        Assert.False((await harness.Queries.GetOrderAsync(new GetOrderQuery("ORD-0002"), TestContext.Current.CancellationToken)).IsSuccess);
    }

    [Fact]
    public async Task CancelOrder_PlacedOrder_ReturnsItCancelled()
    {
        using var harness = new ToolHarness();
        await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k1" });

        var result = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "k2" });

        Assert.Equal("Cancelled", result.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetStock_ForUnknownProduct_ReturnsProductNotFoundNamingItWithoutData()
    {
        using var harness = new ToolHarness();

        var result = await harness.InvokeAsync(SalesToolNames.GetStock, new { sku = "SKU-NOPE" });

        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.False(result.TryGetProperty("data", out _));
        Assert.Equal(ErrorCodes.ProductNotFound, ErrorCode(result));
        Assert.Contains("SKU-NOPE", result.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.False(IsRetryable(result));
    }

    [Fact]
    public async Task GetCustomer_ForUnknownSessionCustomer_ReturnsCustomerNotFound()
    {
        using var harness = new ToolHarness("CUST-999");

        Assert.Equal(ErrorCodes.CustomerNotFound, ErrorCode(await harness.InvokeAsync(SalesToolNames.GetCustomer)));
    }

    [Fact]
    public async Task GetOrder_ForUnknownOrder_ReturnsOrderNotFound()
    {
        using var harness = new ToolHarness();

        Assert.Equal(ErrorCodes.OrderNotFound, ErrorCode(await harness.InvokeAsync(SalesToolNames.GetOrder, new { orderId = "ORD-9999" })));
    }

    public static TheoryData<string, string, string, string> PlaceOrderFailures => new()
    {
        { "CUST-001", """[]""", "k1", ErrorCodes.EmptyOrder },
        { "CUST-001", """[{"sku":"SKU-MUG-RED","quantity":0}]""", "k1", ErrorCodes.InvalidQuantity },
        { "CUST-001", """[{"sku":"SKU-MUG-RED","quantity":1},{"sku":"SKU-MUG-RED","quantity":1}]""", "k1", ErrorCodes.DuplicateOrderLine },
        { "CUST-999", """[{"sku":"SKU-MUG-RED","quantity":1}]""", "k1", ErrorCodes.CustomerNotFound },
        { "CUST-003", """[{"sku":"SKU-MUG-RED","quantity":1}]""", "k1", ErrorCodes.CustomerBlocked },
        { "CUST-001", """[{"sku":"SKU-NOPE","quantity":1}]""", "k1", ErrorCodes.ProductNotFound },
        { "CUST-001", """[{"sku":"SKU-CHAIR-ERGO","quantity":5}]""", "k1", ErrorCodes.OrderLimitExceeded },
        { "CUST-001", """[{"sku":"SKU-TSHIRT-M","quantity":6}]""", "k1", ErrorCodes.InsufficientStock },
        { "CUST-001", """[{"sku":"SKU-MUG-RED","quantity":1}]""", "", ErrorCodes.InvalidIdempotencyKey },
    };

    [Theory]
    [MemberData(nameof(PlaceOrderFailures))]
    public async Task PlaceOrder_BreakingARule_ReturnsTheOperationCodeAsNotRetryable(
        string customerId,
        string linesJson,
        string idempotencyKey,
        string expectedCode)
    {
        using var harness = new ToolHarness(customerId);

        var result = await harness.InvokeAsync(
            SalesToolNames.PlaceOrder,
            new { lines = System.Text.Json.JsonDocument.Parse(linesJson).RootElement, idempotencyKey });

        Assert.Equal(expectedCode, ErrorCode(result));
        Assert.False(IsRetryable(result));
    }

    [Fact]
    public async Task PlaceOrder_ReusingTheKeyWithOtherLines_ReturnsIdempotencyKeyConflict()
    {
        using var harness = new ToolHarness();
        await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k1" });

        var result = await harness.InvokeAsync(
            SalesToolNames.PlaceOrder,
            new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 3 } }, idempotencyKey = "k1" });

        Assert.Equal(ErrorCodes.IdempotencyKeyConflict, ErrorCode(result));
    }

    [Fact]
    public async Task CancelOrder_ForUnknownOrder_ReturnsOrderNotFound()
    {
        using var harness = new ToolHarness();

        var result = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-9999", idempotencyKey = "k1" });

        Assert.Equal(ErrorCodes.OrderNotFound, ErrorCode(result));
    }

    [Fact]
    public async Task CancelOrder_AlreadyCancelledWithAnotherKey_ReturnsInvalidStateTransition()
    {
        using var harness = new ToolHarness();
        await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k0" });
        await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "k1" });

        var result = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "k2" });

        Assert.Equal(ErrorCodes.InvalidStateTransition, ErrorCode(result));
        Assert.False(IsRetryable(result));
    }

    [Fact]
    public async Task CancelOrder_WithMalformedKey_ReturnsInvalidIdempotencyKey()
    {
        using var harness = new ToolHarness();
        await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k0" });

        var result = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "   " });

        Assert.Equal(ErrorCodes.InvalidIdempotencyKey, ErrorCode(result));
    }

    [Fact]
    public async Task CancelOrder_WithTheKeyUsedToPlaceTheOrder_ReturnsIdempotencyKeyConflict()
    {
        using var harness = new ToolHarness();
        await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k0" });

        var result = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "k0" });

        Assert.Equal(ErrorCodes.IdempotencyKeyConflict, ErrorCode(result));
    }

    [Fact]
    public async Task GetStock_WithAnInducedFailure_ReturnsServiceUnavailableAsRetryable()
    {
        using var harness = new ToolHarness(
            "CUST-001",
            new FaultRule { Operation = FaultOperations.GetStock, Trigger = FaultTrigger.Always });

        var result = await harness.InvokeAsync(SalesToolNames.GetStock, new { sku = "SKU-MUG-RED" });

        Assert.Equal(ErrorCodes.ServiceUnavailable, ErrorCode(result));
        Assert.True(IsRetryable(result));
    }

    [Fact]
    public async Task CancelOrder_WithLostResponse_ReturnsRetryableErrorAndTheRetryWithTheSameKeySucceeds()
    {
        using var harness = new ToolHarness(
            "CUST-001",
            new FaultRule
            {
                Operation = FaultOperations.CancelOrder,
                Trigger = FaultTrigger.FirstCalls,
                Count = 1,
                Timing = FaultTiming.After,
            });
        await harness.InvokeAsync(SalesToolNames.PlaceOrder, new { lines = TwoMugs, idempotencyKey = "k0" });

        var lost = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "k1" });
        var cancelledMeanwhile = await harness.Queries.GetOrderAsync(new GetOrderQuery("ORD-0001"), TestContext.Current.CancellationToken);
        var retry = await harness.InvokeAsync(SalesToolNames.CancelOrder, new { orderId = "ORD-0001", idempotencyKey = "k1" });

        Assert.Equal(ErrorCodes.ServiceUnavailable, ErrorCode(lost));
        Assert.True(IsRetryable(lost));
        Assert.Equal(Domain.Sales.OrderStatus.Cancelled, cancelledMeanwhile.Value?.Status);
        Assert.Equal("Cancelled", retry.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task PlaceOrder_LackingStock_IsNotRetryable()
    {
        using var harness = new ToolHarness();

        var result = await harness.InvokeAsync(
            SalesToolNames.PlaceOrder,
            new { lines = new[] { new { sku = "SKU-LAMP-DESK", quantity = 1 } }, idempotencyKey = "k1" });

        Assert.Equal(ErrorCodes.InsufficientStock, ErrorCode(result));
        Assert.False(IsRetryable(result));
    }

    [Fact]
    public async Task AnyTool_WhenTheApplicationThrows_ReturnsInternalErrorAndLogsTheException()
    {
        var failure = new InvalidOperationException("detalle interno");
        using var harness = new ToolHarness("CUST-001", new ThrowingQueries(failure));

        var result = await harness.InvokeAsync(SalesToolNames.GetStock, new { sku = "SKU-MUG-RED" });

        Assert.Equal(ToolErrorCodes.InternalError, ErrorCode(result));
        Assert.False(IsRetryable(result));
        Assert.DoesNotContain("detalle interno", result.GetRawText(), StringComparison.Ordinal);
        Assert.Same(failure, Assert.Single(harness.Logs.Entries, entry => entry.Exception is not null).Exception);
    }
}
