using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>Crea un pedido y reserva su stock. Las líneas se comparan en el orden recibido para la idempotencia.</summary>
public sealed record PlaceOrderCommand(string IdempotencyKey, string CustomerId, IReadOnlyList<OrderLineRequest> Lines);
