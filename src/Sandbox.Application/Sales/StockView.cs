using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>Stock de un producto tal como lo ve quien pide: el disponible, sin existencias ni reservas por separado.</summary>
public sealed record StockView(string Sku, string Name, decimal UnitPrice, int Available)
{
    internal static StockView From(StockItem item) => new(item.Sku, item.Name, item.UnitPrice, item.Available);
}
