namespace Sandbox.Application.Sales;

public sealed record OrderLineView(string Sku, int Quantity, decimal UnitPrice, decimal Amount);
