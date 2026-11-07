using Sandbox.Application.Sales;

namespace Sandbox.Application.Inspection;

/// <summary>
/// Lectura de inspección del estado completo (design D4 de <c>add-scenario-engine</c>). Lee con el cerrojo tomado, no
/// pasa por la autorización por recurso ni por el inyector de fallos y no cambia nada. No se ofrece al modelo: las
/// tools no lo reciben.
/// </summary>
public sealed class SalesInspector(SalesState state)
{
    public Task<SalesSnapshot> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = state.Locked(() => new SalesSnapshot(
            [.. state.Orders.Values.OrderBy(order => order.Id, StringComparer.Ordinal).Select(OrderView.From)],
            [.. state.Stock.Values
                .OrderBy(item => item.Sku, StringComparer.Ordinal)
                .Select(item => new StockLevel(item.Sku, item.OnHand, item.Reserved, item.Available))]));

        return Task.FromResult(snapshot);
    }
}
