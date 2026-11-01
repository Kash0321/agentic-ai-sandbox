namespace Sandbox.Domain.Sales;

/// <summary>Línea de un pedido creado, con el precio unitario fijado al crearlo.</summary>
public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice)
{
    public decimal Amount => Quantity * UnitPrice;
}
