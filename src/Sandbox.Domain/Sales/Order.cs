using Sandbox.Domain.Results;

namespace Sandbox.Domain.Sales;

/// <summary>
/// Pedido de un cliente. Su ciclo de vida es una máquina de estados explícita: se crea en <see cref="OrderStatus.Placed"/>
/// y solo puede pasar a <see cref="OrderStatus.Shipped"/> o a <see cref="OrderStatus.Cancelled"/>. Cada paso aplica su
/// efecto sobre el stock: crear reserva, cancelar libera y enviar consume.
/// </summary>
public sealed class Order
{
    private Order(string id, string customerId, IReadOnlyList<OrderLine> lines, DateTimeOffset placedAt)
    {
        Id = id;
        CustomerId = customerId;
        Lines = lines;
        Total = lines.Sum(line => line.Amount);
        PlacedAt = placedAt;
    }

    public string Id { get; }

    public string CustomerId { get; }

    public IReadOnlyList<OrderLine> Lines { get; }

    public decimal Total { get; }

    public string Currency { get; } = OrderPolicy.Currency;

    public OrderStatus Status { get; private set; } = OrderStatus.Placed;

    public DateTimeOffset PlacedAt { get; }

    /// <summary>
    /// Crea un pedido y reserva su stock. Evalúa en este orden y devuelve el primer error: líneas, cliente bloqueado,
    /// existencia de cada producto, importe máximo y stock de cada línea. Si falla, no reserva nada.
    /// </summary>
    public static Result<Order> Create(
        string id,
        Customer customer,
        IReadOnlyList<OrderLineRequest> requestedLines,
        IReadOnlyDictionary<string, StockItem> stock,
        DateTimeOffset placedAt)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(stock);

        var validation = OrderPolicy.ValidateLines(requestedLines);
        if (!validation.IsSuccess)
        {
            return Result.Failure<Order>(validation.Error);
        }

        if (customer.Status == CustomerStatus.Blocked)
        {
            return Result.Failure<Order>(SalesErrors.CustomerBlocked());
        }

        List<(StockItem Item, int Quantity)> reservations = [];
        foreach (var line in requestedLines)
        {
            if (!stock.TryGetValue(line.Sku, out var item))
            {
                return Result.Failure<Order>(SalesErrors.ProductNotFound(line.Sku));
            }

            reservations.Add((item, line.Quantity));
        }

        var lines = reservations.Select(r => new OrderLine(r.Item.Sku, r.Quantity, r.Item.UnitPrice)).ToList();
        if (lines.Sum(line => line.Amount) > OrderPolicy.MaxOrderTotal)
        {
            return Result.Failure<Order>(SalesErrors.OrderLimitExceeded());
        }

        // Todo o nada: se comprueban todas las líneas antes de reservar la primera.
        foreach (var (item, quantity) in reservations)
        {
            if (!item.CanReserve(quantity))
            {
                return Result.Failure<Order>(SalesErrors.InsufficientStock(item.Sku, item.Available));
            }
        }

        foreach (var (item, quantity) in reservations)
        {
            item.Reserve(quantity);
        }

        return Result.Success(new Order(id, customer.Id, lines, placedAt));
    }

    /// <summary>Envía un pedido en curso y consume su stock reservado.</summary>
    public Result<Order> Ship(IReadOnlyDictionary<string, StockItem> stock) =>
        LeavePlaced(OrderStatus.Shipped, "enviar", stock, static (item, quantity) => item.Consume(quantity));

    /// <summary>Cancela un pedido en curso y libera su stock reservado.</summary>
    public Result<Order> Cancel(IReadOnlyDictionary<string, StockItem> stock) =>
        LeavePlaced(OrderStatus.Cancelled, "cancelar", stock, static (item, quantity) => item.Release(quantity));

    private Result<Order> LeavePlaced(
        OrderStatus target,
        string action,
        IReadOnlyDictionary<string, StockItem> stock,
        Action<StockItem, int> applyToStock)
    {
        ArgumentNullException.ThrowIfNull(stock);

        if (Status != OrderStatus.Placed)
        {
            return Result.Failure<Order>(SalesErrors.InvalidStateTransition(action));
        }

        foreach (var line in Lines)
        {
            applyToStock(stock[line.Sku], line.Quantity);
        }

        Status = target;
        return Result.Success(this);
    }
}
