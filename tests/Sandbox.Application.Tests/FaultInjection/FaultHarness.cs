using Microsoft.Extensions.Options;
using Sandbox.Application.FaultInjection;
using Sandbox.Application.Sales;
using Sandbox.Application.Tests.Fakes;
using Sandbox.Application.Tests.Sales;
using Sandbox.Domain.Results;

namespace Sandbox.Application.Tests.FaultInjection;

/// <summary>
/// Commands y queries decorados con el inyector, sobre el catálogo semilla y con el mismo reloj falso que el estado.
/// <see cref="Sales"/> da acceso a las operaciones sin inyector, para comprobar el estado real.
/// </summary>
internal sealed class FaultHarness
{
    public const int DefaultSeed = 42;

    public FaultHarness(params FaultRule[] rules)
        : this(DefaultSeed, rules)
    {
    }

    public FaultHarness(int seed, params FaultRule[] rules)
    {
        var options = new FaultInjectionOptions { Seed = seed };
        foreach (var rule in rules)
        {
            options.Rules.Add(rule);
        }

        var injector = new FaultInjector(Options.Create(options), Sales.Clock, Logger);
        Commands = new FaultInjectingSalesCommands(Sales.Commands, injector);
        Queries = new FaultInjectingSalesQueries(Sales.Queries, injector);
    }

    public SalesHarness Sales { get; } = new();

    public RecordingLogger<FaultInjector> Logger { get; } = new();

    public ISalesCommands Commands { get; }

    public ISalesQueries Queries { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static FaultRule Rule(
        string operation,
        FaultTrigger trigger = FaultTrigger.None,
        int? count = null,
        double? probability = null,
        FaultTiming timing = FaultTiming.Before,
        TimeSpan latency = default) =>
        new()
        {
            Operation = operation,
            Trigger = trigger,
            Count = count,
            Probability = probability,
            Timing = timing,
            Latency = latency,
        };

    public Task<Result<OrderView>> PlaceAsync(string key, CancellationToken? cancellationToken = null) =>
        Commands.PlaceOrderAsync(
            new PlaceOrderCommand(key, "CUST-001", [SalesHarness.Line("SKU-MUG-RED", 2)]),
            cancellationToken ?? Token);

    public Task<Result<OrderView>> CancelAsync(string key, string orderId) =>
        Commands.CancelOrderAsync(new CancelOrderCommand(key, orderId), Token);

    public Task<Result<OrderView>> ShipAsync(string key, string orderId) =>
        Commands.ShipOrderAsync(new ShipOrderCommand(key, orderId), Token);

    public Task<Result<OrderView>> GetOrderAsync(string orderId) =>
        Queries.GetOrderAsync(new GetOrderQuery(orderId), Token);

    public Task<Result<CustomerView>> GetCustomerAsync(string customerId) =>
        Queries.GetCustomerAsync(new GetCustomerQuery(customerId), Token);

    public Task<Result<StockView>> GetStockAsync(string sku) =>
        Queries.GetStockAsync(new GetStockQuery(sku), Token);

    /// <summary>Patrón de fallos de <paramref name="calls"/> llamadas seguidas: <see langword="true"/> si falló.</summary>
    public static async Task<List<bool>> FailurePatternAsync(int calls, Func<Task<bool>> call)
    {
        List<bool> pattern = [];
        for (var i = 0; i < calls; i++)
        {
            pattern.Add(await call());
        }

        return pattern;
    }
}
