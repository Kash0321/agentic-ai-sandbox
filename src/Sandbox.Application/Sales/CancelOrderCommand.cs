namespace Sandbox.Application.Sales;

/// <summary>Cancela un pedido en curso del cliente que lo pide y libera su stock.</summary>
public sealed record CancelOrderCommand(string IdempotencyKey, string OrderId, string CustomerId);
