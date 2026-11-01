using Sandbox.Application.Sales;
using Sandbox.Domain.Results;

namespace Sandbox.Application.FaultInjection;

/// <summary>Decorador que pasa cada command por el <see cref="FaultInjector"/>.</summary>
public sealed class FaultInjectingSalesCommands(ISalesCommands inner, FaultInjector injector) : ISalesCommands
{
    public Task<Result<OrderView>> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default) =>
        injector.RunAsync(FaultOperations.PlaceOrder, token => inner.PlaceOrderAsync(command, token), cancellationToken);

    public Task<Result<OrderView>> CancelOrderAsync(CancelOrderCommand command, CancellationToken cancellationToken = default) =>
        injector.RunAsync(FaultOperations.CancelOrder, token => inner.CancelOrderAsync(command, token), cancellationToken);

    public Task<Result<OrderView>> ShipOrderAsync(ShipOrderCommand command, CancellationToken cancellationToken = default) =>
        injector.RunAsync(FaultOperations.ShipOrder, token => inner.ShipOrderAsync(command, token), cancellationToken);
}
