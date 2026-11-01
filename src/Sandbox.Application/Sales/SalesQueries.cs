using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>Queries sobre el estado in-memory. Leen con el cerrojo tomado para ver un estado coherente.</summary>
public sealed class SalesQueries(SalesState state) : ISalesQueries
{
    public Task<Result<OrderView>> GetOrderAsync(GetOrderQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Find(state.Orders, query.OrderId, OrderView.From, SalesErrors.OrderNotFound, cancellationToken);
    }

    public Task<Result<CustomerView>> GetCustomerAsync(GetCustomerQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Find(state.Customers, query.CustomerId, CustomerView.From, SalesErrors.CustomerNotFound, cancellationToken);
    }

    public Task<Result<StockView>> GetStockAsync(GetStockQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Find(state.Stock, query.Sku, StockView.From, () => SalesErrors.ProductNotFound(query.Sku), cancellationToken);
    }

    private Task<Result<TView>> Find<TEntity, TView>(
        Dictionary<string, TEntity> source,
        string id,
        Func<TEntity, TView> toView,
        Func<DomainError> notFound,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = state.Locked(() => source.TryGetValue(id, out var entity)
            ? Result.Success(toView(entity))
            : Result.Failure<TView>(notFound()));

        return Task.FromResult(result);
    }
}
