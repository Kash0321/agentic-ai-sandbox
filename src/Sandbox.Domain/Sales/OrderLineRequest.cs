namespace Sandbox.Domain.Sales;

/// <summary>Línea pedida: producto y cantidad, todavía sin validar ni precio.</summary>
public sealed record OrderLineRequest(string Sku, int Quantity);
