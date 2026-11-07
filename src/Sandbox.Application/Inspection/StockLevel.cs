namespace Sandbox.Application.Inspection;

/// <summary>Stock de un producto tal como lo ve quien evalúa: existencias, lo reservado y el disponible.</summary>
public sealed record StockLevel(string Sku, int OnHand, int Reserved, int Available);
