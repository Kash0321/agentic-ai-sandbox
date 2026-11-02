using Sandbox.Application.Sales;
using Sandbox.Domain.Results;

namespace Sandbox.Agents.Tests.Tools;

/// <summary>Queries que fallan de forma imprevista: sirven para probar el borde de las tools.</summary>
internal sealed class ThrowingQueries(Exception failure) : ISalesQueries
{
    public Task<Result<OrderView>> GetOrderAsync(GetOrderQuery query, CancellationToken cancellationToken = default) =>
        throw failure;

    public Task<Result<CustomerView>> GetCustomerAsync(GetCustomerQuery query, CancellationToken cancellationToken = default) =>
        throw failure;

    public Task<Result<StockView>> GetStockAsync(GetStockQuery query, CancellationToken cancellationToken = default) =>
        throw failure;

    public Task<Result<IReadOnlyList<StockView>>> ListProductsAsync(
        ListProductsQuery query,
        CancellationToken cancellationToken = default) =>
        throw failure;
}
