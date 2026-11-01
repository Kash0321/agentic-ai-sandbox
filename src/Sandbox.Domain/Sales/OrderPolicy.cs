using Sandbox.Domain.Results;

namespace Sandbox.Domain.Sales;

/// <summary>Límites de negocio de un pedido. Son reglas del dominio, no configuración.</summary>
public static class OrderPolicy
{
    public const int MinQuantity = 1;

    public const int MaxQuantity = 100;

    public const decimal MaxOrderTotal = 1000.00m;

    public const string Currency = "EUR";

    /// <summary>
    /// Valida las líneas en este orden: que haya alguna, que cada cantidad esté en rango y que ningún producto se repita.
    /// </summary>
    public static Result<IReadOnlyList<OrderLineRequest>> ValidateLines(IReadOnlyList<OrderLineRequest> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            return Result.Failure<IReadOnlyList<OrderLineRequest>>(SalesErrors.EmptyOrder());
        }

        if (lines.Any(line => line.Quantity is < MinQuantity or > MaxQuantity))
        {
            return Result.Failure<IReadOnlyList<OrderLineRequest>>(SalesErrors.InvalidQuantity());
        }

        if (lines.DistinctBy(line => line.Sku, StringComparer.Ordinal).Count() != lines.Count)
        {
            return Result.Failure<IReadOnlyList<OrderLineRequest>>(SalesErrors.DuplicateOrderLine());
        }

        return Result.Success(lines);
    }
}
