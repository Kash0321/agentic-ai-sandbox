using System.Globalization;
using Sandbox.Domain.Results;

namespace Sandbox.Domain.Sales;

/// <summary>
/// Errores del dominio de ventas. Los mensajes son breves y dicen cómo corregir la petición; solo llevan los datos que
/// necesita quien la corrige (el producto y su disponible).
/// </summary>
public static class SalesErrors
{
    public static DomainError EmptyOrder() =>
        new(ErrorCodes.EmptyOrder, "El pedido debe tener al menos una línea.", ErrorKind.Validation);

    public static DomainError InvalidQuantity() =>
        new(
            ErrorCodes.InvalidQuantity,
            $"La cantidad de cada línea debe ser un entero entre {OrderPolicy.MinQuantity} y {OrderPolicy.MaxQuantity}.",
            ErrorKind.Validation);

    public static DomainError DuplicateOrderLine() =>
        new(
            ErrorCodes.DuplicateOrderLine,
            "Cada producto solo puede aparecer en una línea; suma sus cantidades en una sola.",
            ErrorKind.Validation);

    public static DomainError CustomerBlocked() =>
        new(ErrorCodes.CustomerBlocked, "El cliente está bloqueado y no puede hacer pedidos.", ErrorKind.BusinessRule);

    public static DomainError OrderLimitExceeded() =>
        new(
            ErrorCodes.OrderLimitExceeded,
            string.Create(
                CultureInfo.InvariantCulture,
                $"El importe del pedido supera el máximo de {OrderPolicy.MaxOrderTotal:F2} {OrderPolicy.Currency}."),
            ErrorKind.BusinessRule);

    public static DomainError InsufficientStock(string sku, int available) =>
        new(
            ErrorCodes.InsufficientStock,
            string.Create(
                CultureInfo.InvariantCulture,
                $"No hay stock suficiente de {sku}: quedan {available} unidades disponibles."),
            ErrorKind.BusinessRule);

    public static DomainError InvalidStateTransition(string action) =>
        new(
            ErrorCodes.InvalidStateTransition,
            $"Solo se puede {action} un pedido en estado {OrderStatus.Placed}.",
            ErrorKind.Conflict);

    public static DomainError CustomerNotFound() =>
        new(ErrorCodes.CustomerNotFound, "No existe ningún cliente con ese identificador.", ErrorKind.NotFound);

    public static DomainError ProductNotFound(string sku) =>
        new(ErrorCodes.ProductNotFound, $"No existe el producto {sku}.", ErrorKind.NotFound);

    public static DomainError OrderNotFound() =>
        new(ErrorCodes.OrderNotFound, "No existe ningún pedido con ese identificador.", ErrorKind.NotFound);
}
