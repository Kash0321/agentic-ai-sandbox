using Sandbox.Domain.Sales;

namespace Sandbox.Runner.Scenarios;

/// <summary>Pedido que un escenario necesita que exista antes de atender la petición: su cliente y sus líneas.</summary>
public sealed record SeedOrder(string CustomerId, IReadOnlyList<OrderLineRequest> Lines);
