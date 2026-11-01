namespace Sandbox.Domain.Sales;

/// <summary>Estados del pedido. Solo hay dos transiciones: <c>Placed -> Shipped</c> y <c>Placed -> Cancelled</c>.</summary>
public enum OrderStatus
{
    Placed,
    Shipped,
    Cancelled,
}
