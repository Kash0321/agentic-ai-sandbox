using Sandbox.Application.FaultInjection;
using Sandbox.Application.Tests.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;
using static Sandbox.Application.Tests.FaultInjection.FaultHarness;

namespace Sandbox.Application.Tests.FaultInjection;

public sealed class FaultInjectorTests
{
    private const int Calls = 20;

    [Fact]
    public async Task Operations_WithoutRules_BehaveAsWithoutInjectorAndNeverWait()
    {
        var injected = new FaultHarness();
        var plain = new SalesHarness();

        var token = TestContext.Current.CancellationToken;

        var placing = injected.PlaceAsync("k1");
        Assert.True(placing.IsCompleted);
        var injectedPlace = await placing;
        var plainPlace = await plain.PlaceAsync("k1", "CUST-001", SalesHarness.Line("SKU-MUG-RED", 2));

        Assert.Equal(plainPlace.Value?.OrderId, injectedPlace.Value?.OrderId);
        Assert.Equal(plainPlace.Value?.Total, injectedPlace.Value?.Total);
        Assert.Equal(
            (await plain.Queries.GetStockAsync(new("SKU-MUG-RED"), token)).Value,
            (await injected.GetStockAsync("SKU-MUG-RED")).Value);
        Assert.Equal(
            (await plain.Queries.GetCustomerAsync(new("CUST-003"), token)).Value,
            (await injected.GetCustomerAsync("CUST-003")).Value);
        Assert.Equal((await plain.CancelAsync("k2", "ORD-0001")).Value?.Status, (await injected.CancelAsync("k2", "ORD-0001")).Value?.Status);
        Assert.Equal((await plain.ShipAsync("k3", "ORD-0001")).Error, (await injected.ShipAsync("k3", "ORD-0001")).Error);
        Assert.Empty(injected.Logger.Entries);
    }

    [Fact]
    public async Task Rule_OnOneOperation_DoesNotAffectTheOthers()
    {
        var harness = new FaultHarness(Rule(FaultOperations.GetStock, FaultTrigger.Always));
        await harness.PlaceAsync("k1");

        var stock = await harness.GetStockAsync("SKU-MUG-RED");
        var order = await harness.GetOrderAsync("ORD-0001");

        ResultAssert.Fails(stock, ErrorCodes.ServiceUnavailable);
        Assert.True(order.IsSuccess);
    }

    [Fact]
    public async Task Rule_OnEveryOperation_MakesAllOfThemFail()
    {
        var harness = new FaultHarness(Rule(FaultOperations.All, FaultTrigger.Always));

        ResultAssert.Fails(await harness.PlaceAsync("k1"), ErrorCodes.ServiceUnavailable);
        ResultAssert.Fails(await harness.CancelAsync("k2", "ORD-0001"), ErrorCodes.ServiceUnavailable);
        ResultAssert.Fails(await harness.ShipAsync("k3", "ORD-0001"), ErrorCodes.ServiceUnavailable);
        ResultAssert.Fails(await harness.GetOrderAsync("ORD-0001"), ErrorCodes.ServiceUnavailable);
        ResultAssert.Fails(await harness.GetCustomerAsync("CUST-001"), ErrorCodes.ServiceUnavailable);
        ResultAssert.Fails(await harness.GetStockAsync("SKU-MUG-RED"), ErrorCodes.ServiceUnavailable);
    }

    [Fact]
    public async Task AlwaysRule_OnPlaceOrder_FailsEveryCall()
    {
        var harness = new FaultHarness(Rule(FaultOperations.PlaceOrder, FaultTrigger.Always));

        var pattern = await FailurePatternAsync(3, async () => !(await harness.PlaceAsync("k1")).IsSuccess);

        Assert.Equal([true, true, true], pattern);
    }

    [Fact]
    public async Task FirstCallsRule_WithTwo_FailsOnlyTheFirstTwoCalls()
    {
        var harness = new FaultHarness(Rule(FaultOperations.GetOrder, FaultTrigger.FirstCalls, count: 2));
        await harness.PlaceAsync("k1");

        var pattern = await FailurePatternAsync(4, async () => !(await harness.GetOrderAsync("ORD-0001")).IsSuccess);

        Assert.Equal([true, true, false, false], pattern);
    }

    [Fact]
    public async Task ProbabilityRule_WithTheSameSeed_FailsOnTheSameCalls()
    {
        var first = new FaultHarness(Rule(FaultOperations.GetStock, FaultTrigger.Probability, probability: 0.5));
        var second = new FaultHarness(Rule(FaultOperations.GetStock, FaultTrigger.Probability, probability: 0.5));

        var firstPattern = await FailurePatternAsync(Calls, () => StockFailsAsync(first));
        var secondPattern = await FailurePatternAsync(Calls, () => StockFailsAsync(second));

        Assert.Equal(firstPattern, secondPattern);
    }

    [Fact]
    public async Task ProbabilityRule_WithCallsToOtherOperationsInBetween_KeepsItsPattern()
    {
        var alone = new FaultHarness(Rule(FaultOperations.GetStock, FaultTrigger.Probability, probability: 0.5));
        var interleaved = new FaultHarness(Rule(FaultOperations.GetStock, FaultTrigger.Probability, probability: 0.5));

        var alonePattern = await FailurePatternAsync(Calls, () => StockFailsAsync(alone));
        var interleavedPattern = await FailurePatternAsync(Calls, async () =>
        {
            await interleaved.GetOrderAsync("ORD-0001");
            return await StockFailsAsync(interleaved);
        });

        Assert.Equal(alonePattern, interleavedPattern);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(1.0, true)]
    public async Task ProbabilityRule_AtTheExtremes_NeverOrAlwaysFails(double probability, bool expectedFailure)
    {
        var harness = new FaultHarness(Rule(FaultOperations.GetStock, FaultTrigger.Probability, probability: probability));

        var pattern = await FailurePatternAsync(Calls, () => StockFailsAsync(harness));

        Assert.All(pattern, failed => Assert.Equal(expectedFailure, failed));
    }

    [Fact]
    public async Task InducedFailure_Always_IsServiceUnavailableAndTransient()
    {
        var harness = new FaultHarness(Rule(FaultOperations.GetCustomer, FaultTrigger.Always));

        var result = await harness.GetCustomerAsync("CUST-001");

        Assert.Equal(ErrorKind.Transient, ResultAssert.Fails(result, ErrorCodes.ServiceUnavailable).Kind);
    }

    [Fact]
    public async Task BeforeRule_OnPlaceOrder_FailsWithoutCreatingTheOrderOrReserving()
    {
        var harness = new FaultHarness(Rule(FaultOperations.PlaceOrder, FaultTrigger.FirstCalls, count: 1));

        var result = await harness.PlaceAsync("k1");

        ResultAssert.Fails(result, ErrorCodes.ServiceUnavailable);
        Assert.False(await harness.Sales.OrderExistsAsync("ORD-0001"));
        Assert.Equal(20, await harness.Sales.AvailableAsync("SKU-MUG-RED"));
    }

    [Fact]
    public async Task AfterRule_OnCancelOrder_KeepsTheEffectsAndTheRetryWithTheSameKeySucceeds()
    {
        var harness = new FaultHarness(
            Rule(FaultOperations.CancelOrder, FaultTrigger.FirstCalls, count: 1, timing: FaultTiming.After));
        await harness.PlaceAsync("k0");

        var lost = await harness.CancelAsync("k1", "ORD-0001");
        var cancelledMeanwhile = await harness.Sales.OrderAsync("ORD-0001");
        var availableMeanwhile = await harness.Sales.AvailableAsync("SKU-MUG-RED");
        var retry = await harness.CancelAsync("k1", "ORD-0001");

        ResultAssert.Fails(lost, ErrorCodes.ServiceUnavailable);
        Assert.Equal(OrderStatus.Cancelled, cancelledMeanwhile.Status);
        Assert.Equal(20, availableMeanwhile);
        Assert.Equal(OrderStatus.Cancelled, retry.Value?.Status);
    }

    [Fact]
    public async Task Rules_WithGlobalLatencyAndAFailingRule_WaitTheLatencyAndFail()
    {
        var harness = new FaultHarness(
            Rule(FaultOperations.All, latency: TimeSpan.FromMilliseconds(100)),
            Rule(FaultOperations.ShipOrder, FaultTrigger.Always));

        var shipping = harness.ShipAsync("k1", "ORD-0001");
        Assert.False(shipping.IsCompleted);
        harness.Sales.Clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.False(shipping.IsCompleted);
        harness.Sales.Clock.Advance(TimeSpan.FromMilliseconds(1));

        ResultAssert.Fails(await shipping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken), ErrorCodes.ServiceUnavailable);
    }

    private static async Task<bool> StockFailsAsync(FaultHarness harness) =>
        !(await harness.GetStockAsync("SKU-MUG-RED")).IsSuccess;
}
