using Sandbox.Application.Sales;
using Sandbox.Domain.Results;

namespace Sandbox.Application.FaultInjection;

/// <summary>Decorador que pasa cada query por el <see cref="FaultInjector"/>.</summary>
public sealed class FaultInjectingSalesQueries(ISalesQueries inner, FaultInjector injector) : ISalesQueries
{
    public Task<Result<OrderView>> GetOrderAsync(GetOrderQuery query, CancellationToken cancellationToken = default) =>
        injector.RunAsync(FaultOperations.GetOrder, token => inner.GetOrderAsync(query, token), cancellationToken);

    public Task<Result<CustomerView>> GetCustomerAsync(GetCustomerQuery query, CancellationToken cancellationToken = default) =>
        injector.RunAsync(FaultOperations.GetCustomer, token => inner.GetCustomerAsync(query, token), cancellationToken);

    public Task<Result<StockView>> GetStockAsync(GetStockQuery query, CancellationToken cancellationToken = default) =>
        injector.RunAsync(FaultOperations.GetStock, token => inner.GetStockAsync(query, token), cancellationToken);
}
