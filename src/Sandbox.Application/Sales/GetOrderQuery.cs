namespace Sandbox.Application.Sales;

/// <summary>Consulta un pedido en nombre de un cliente: solo ve los suyos.</summary>
public sealed record GetOrderQuery(string OrderId, string CustomerId);
