using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using static Sandbox.Application.Tests.Sales.SalesHarness;

namespace Sandbox.Application.Tests.Sales;

public sealed class ConcurrencyAndCancellationTests
{
    private const int Repetitions = 100;

    [Fact]
    public async Task PlaceOrder_TwoConcurrentCallsForTheLastUnits_ExactlyOneWins()
    {
        for (var attempt = 0; attempt < Repetitions; attempt++)
        {
            var harness = new SalesHarness(StateWith(Product("SKU-A", onHand: 5)));
            using var start = new ManualResetEventSlim();

            var first = Task.Run(() => PlaceWhenReleased(harness, start, "k1"), TestContext.Current.CancellationToken);
            var second = Task.Run(() => PlaceWhenReleased(harness, start, "k2"), TestContext.Current.CancellationToken);
            start.Set();
            var results = await Task.WhenAll(first, second);

            Assert.Single(results, result => result.IsSuccess);
            Assert.Single(results, result => result.Error?.Code == ErrorCodes.InsufficientStock);
            Assert.Equal(0, await harness.AvailableAsync("SKU-A"));
            Assert.Equal("ORD-0001", results.Single(result => result.IsSuccess).Value?.OrderId);
        }
    }

    [Fact]
    public async Task PlaceOrder_WithAlreadyCancelledToken_ThrowsAndCreatesNoOrder()
    {
        var harness = new SalesHarness();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var command = new PlaceOrderCommand("k1", "CUST-001", [Line("SKU-MUG-RED", 1)]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Commands.PlaceOrderAsync(command, cancelled.Token));

        Assert.False(await harness.OrderExistsAsync("ORD-0001"));
        Assert.Equal(20, await harness.AvailableAsync("SKU-MUG-RED"));
    }

    private static Task<Result<OrderView>> PlaceWhenReleased(SalesHarness harness, ManualResetEventSlim start, string key)
    {
        start.Wait(TestContext.Current.CancellationToken);
        return harness.Commands.PlaceOrderAsync(
            new PlaceOrderCommand(key, ActiveCustomerId, [Line("SKU-A", 5)]),
            TestContext.Current.CancellationToken);
    }
}
