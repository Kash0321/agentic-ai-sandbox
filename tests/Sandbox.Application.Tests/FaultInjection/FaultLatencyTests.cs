using Sandbox.Application.FaultInjection;
using static Sandbox.Application.Tests.FaultInjection.FaultHarness;

namespace Sandbox.Application.Tests.FaultInjection;

/// <summary>
/// Tests con latencia sobre un reloj falso. Patrón obligatorio (design D8): lanzar la llamada sin esperarla, comprobar
/// que no ha terminado, avanzar el reloj y después esperarla. Un <c>await</c> antes de avanzar el reloj se queda
/// colgado, y el <c>Timeout</c> convierte ese cuelgue en un fallo.
/// </summary>
public sealed class FaultLatencyTests
{
    private const int TimeoutMilliseconds = 10_000;

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LatencyRule_OnGetOrder_WaitsForTheClockAndThenResponds()
    {
        var harness = new FaultHarness(Rule(FaultOperations.GetOrder, latency: TimeSpan.FromMilliseconds(200)));

        var getting = harness.GetOrderAsync("ORD-9999");
        Assert.False(getting.IsCompleted);
        harness.Sales.Clock.Advance(TimeSpan.FromMilliseconds(199));
        Assert.False(getting.IsCompleted);
        harness.Sales.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var result = await getting.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Domain.Results.ErrorCodes.OrderNotFound, result.Error?.Code);
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LatencyRule_CancelledWhileWaiting_ThrowsAndCreatesNoOrder()
    {
        var harness = new FaultHarness(Rule(FaultOperations.PlaceOrder, latency: TimeSpan.FromSeconds(5)));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var placing = harness.PlaceAsync("k1", cancellation.Token);
        Assert.False(placing.IsCompleted);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => placing);
        Assert.False(await harness.Sales.OrderExistsAsync("ORD-0001"));
        Assert.Equal(20, await harness.Sales.AvailableAsync("SKU-MUG-RED"));
    }
}
