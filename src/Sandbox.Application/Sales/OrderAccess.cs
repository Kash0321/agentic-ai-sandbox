using Sandbox.Application.Auditing;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>
/// Autorización por recurso (design D1 y D2 de <c>add-agent-guardrails</c>): un cliente solo accede a sus pedidos, y un
/// pedido ajeno se responde igual que uno inexistente para no confirmar que existe. El motivo real queda en la
/// auditoría. Se llama con el cerrojo de <see cref="SalesState"/> tomado, así que comprobar y actuar son atómicos.
/// </summary>
internal static class OrderAccess
{
    public static Result<Order> FindOwned(
        SalesState state,
        string orderId,
        string customerId,
        string operation,
        AuditLog audit)
    {
        if (!state.Orders.TryGetValue(orderId, out var order))
        {
            return Result.Failure<Order>(SalesErrors.OrderNotFound());
        }

        if (!string.Equals(order.CustomerId, customerId, StringComparison.Ordinal))
        {
            audit.AccessDenied(operation, orderId, customerId, AuditLog.ForeignOrderReason);
            return Result.Failure<Order>(SalesErrors.OrderNotFound());
        }

        return Result.Success(order);
    }
}
