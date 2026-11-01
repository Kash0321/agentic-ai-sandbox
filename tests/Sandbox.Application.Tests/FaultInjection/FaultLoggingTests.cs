using Microsoft.Extensions.Logging;
using Sandbox.Application.FaultInjection;
using static Sandbox.Application.Tests.FaultInjection.FaultHarness;

namespace Sandbox.Application.Tests.FaultInjection;

public sealed class FaultLoggingTests
{
    [Fact]
    public async Task InducedFailure_AfterPlaceOrder_LogsOperationRuleAndTimingWithoutArguments()
    {
        var harness = new FaultHarness(Rule(FaultOperations.PlaceOrder, FaultTrigger.Always, timing: FaultTiming.After));

        await harness.PlaceAsync("k1");

        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(FaultOperations.PlaceOrder, entry.Properties["Operation"]);
        Assert.Equal(0, entry.Properties["Rule"]);
        Assert.Equal(FaultTiming.After, entry.Properties["Timing"]);
        Assert.Equal(0d, entry.Properties["LatencyMs"]);
        var logged = entry.Message + string.Join(" ", entry.Properties.Values);
        Assert.DoesNotContain("CUST-001", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("SKU-MUG-RED", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("k1", logged, StringComparison.Ordinal);
    }

    [Fact(Timeout = 10_000)]
    public async Task InducedFailure_WithLatency_LogsTheLatencyApplied()
    {
        var harness = new FaultHarness(
            Rule(FaultOperations.All, latency: TimeSpan.FromMilliseconds(100)),
            Rule(FaultOperations.ShipOrder, FaultTrigger.Always));

        var shipping = harness.ShipAsync("k1", "ORD-0001");
        harness.Sales.Clock.Advance(TimeSpan.FromMilliseconds(100));
        await shipping.WaitAsync(TestContext.Current.CancellationToken);

        var fault = Assert.Single(harness.Logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal(1, fault.Properties["Rule"]);
        Assert.Equal(FaultTiming.Before, fault.Properties["Timing"]);
        Assert.Equal(100d, fault.Properties["LatencyMs"]);
    }

    [Fact]
    public async Task InducedLatency_OnGetOrder_LogsOperationRulesAndMilliseconds()
    {
        var harness = new FaultHarness(
            Rule(FaultOperations.All, latency: TimeSpan.FromMilliseconds(100)),
            Rule(FaultOperations.GetOrder, latency: TimeSpan.FromMilliseconds(50)));

        var getting = harness.GetOrderAsync("ORD-9999");
        harness.Sales.Clock.Advance(TimeSpan.FromMilliseconds(150));
        await getting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var entry = Assert.Single(harness.Logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(150d, entry.Properties["LatencyMs"]);
        // LoggerMessage formatea las colecciones entre corchetes (observado en el apply).
        Assert.Equal("Latencia inducida en GetOrder: 150 ms de las reglas [0, 1]", entry.Message);
    }
}
