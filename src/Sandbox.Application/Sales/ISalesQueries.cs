using Sandbox.Domain.Results;

namespace Sandbox.Application.Sales;

/// <summary>Operaciones de solo lectura sobre el estado de ventas. Nunca cambian nada.</summary>
public interface ISalesQueries
{
    Task<Result<OrderView>> GetOrderAsync(GetOrderQuery query, CancellationToken cancellationToken = default);

    Task<Result<CustomerView>> GetCustomerAsync(GetCustomerQuery query, CancellationToken cancellationToken = default);

    Task<Result<StockView>> GetStockAsync(GetStockQuery query, CancellationToken cancellationToken = default);
}
