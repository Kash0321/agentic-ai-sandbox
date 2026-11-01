using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>Vista compacta de un pedido, con solo lo que necesita quien la consume.</summary>
public sealed record OrderView(
    string OrderId,
    string CustomerId,
    OrderStatus Status,
    IReadOnlyList<OrderLineView> Lines,
    decimal Total,
    string Currency,
    DateTimeOffset PlacedAt)
{
    internal static OrderView From(Order order) =>
        new(
            order.Id,
            order.CustomerId,
            order.Status,
            [.. order.Lines.Select(line => new OrderLineView(line.Sku, line.Quantity, line.UnitPrice, line.Amount))],
            order.Total,
            order.Currency,
            order.PlacedAt);
}
