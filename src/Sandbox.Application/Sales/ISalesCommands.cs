using Sandbox.Domain.Results;

namespace Sandbox.Application.Sales;

/// <summary>
/// Operaciones que cambian el estado de ventas. Todas exigen una clave de idempotencia: repetir la misma petición con
/// la misma clave devuelve el resultado de la primera vez sin repetir sus efectos.
/// </summary>
public interface ISalesCommands
{
    Task<Result<OrderView>> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default);

    Task<Result<OrderView>> CancelOrderAsync(CancelOrderCommand command, CancellationToken cancellationToken = default);

    Task<Result<OrderView>> ShipOrderAsync(ShipOrderCommand command, CancellationToken cancellationToken = default);
}
