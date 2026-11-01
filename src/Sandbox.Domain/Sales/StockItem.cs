using Sandbox.Domain.Results;

namespace Sandbox.Domain.Sales;

/// <summary>
/// Stock de un producto: existencias físicas y unidades reservadas por pedidos en curso. El disponible es la
/// diferencia.
/// </summary>
public sealed class StockItem(string sku, string name, decimal unitPrice, int onHand)
{
    public string Sku { get; } = sku;

    public string Name { get; } = name;

    public decimal UnitPrice { get; } = unitPrice;

    public int OnHand { get; private set; } = onHand;

    public int Reserved { get; private set; }

    public int Available => OnHand - Reserved;

    public bool CanReserve(int quantity) => quantity <= Available;

    /// <summary>Reserva unidades para un pedido, o devuelve <c>INSUFFICIENT_STOCK</c> sin reservar nada.</summary>
    public Result<StockItem> Reserve(int quantity)
    {
        if (!CanReserve(quantity))
        {
            return Result.Failure<StockItem>(SalesErrors.InsufficientStock(Sku, Available));
        }

        Reserved += quantity;
        return Result.Success(this);
    }

    /// <summary>Devuelve al disponible unidades reservadas por un pedido cancelado.</summary>
    public void Release(int quantity)
    {
        EnsureReserved(quantity);
        Reserved -= quantity;
    }

    /// <summary>Descuenta de existencias y reservas las unidades de un pedido enviado; el disponible no cambia.</summary>
    public void Consume(int quantity)
    {
        EnsureReserved(quantity);
        Reserved -= quantity;
        OnHand -= quantity;
    }

    // Liberar o consumir más de lo reservado no es una regla de negocio incumplida, sino un error de programación:
    // solo los pedidos lo hacen, y siempre con la cantidad que reservaron.
    private void EnsureReserved(int quantity)
    {
        if (quantity > Reserved)
        {
            throw new InvalidOperationException(
                $"No se pueden liberar ni consumir {quantity} unidades de {Sku}: solo hay {Reserved} reservadas.");
        }
    }
}
