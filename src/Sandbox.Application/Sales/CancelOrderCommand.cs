namespace Sandbox.Application.Sales;

/// <summary>Cancela un pedido en curso y libera su stock.</summary>
public sealed record CancelOrderCommand(string IdempotencyKey, string OrderId);
