namespace Sandbox.Application.Sales;

/// <summary>Envía un pedido en curso y consume su stock.</summary>
public sealed record ShipOrderCommand(string IdempotencyKey, string OrderId);
