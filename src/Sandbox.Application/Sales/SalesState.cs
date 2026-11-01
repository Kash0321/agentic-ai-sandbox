using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>
/// Estado in-memory del dominio de ventas: clientes, stock, pedidos, numeración y almacén de idempotencia. Todo se
/// lee y se modifica bajo un único cerrojo, de modo que cada operación es atómica (design D5). El estado vive lo que
/// dura el proceso.
/// </summary>
public sealed class SalesState
{
    private readonly Lock _gate = new();
    private int _lastOrderNumber;

    public SalesState(IEnumerable<Customer> customers, IEnumerable<StockItem> stock)
    {
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(stock);

        Customers = customers.ToDictionary(customer => customer.Id, StringComparer.Ordinal);
        Stock = stock.ToDictionary(item => item.Sku, StringComparer.Ordinal);
    }

    internal Dictionary<string, Customer> Customers { get; }

    internal Dictionary<string, StockItem> Stock { get; }

    internal Dictionary<string, Order> Orders { get; } = new(StringComparer.Ordinal);

    internal IdempotencyStore Idempotency { get; } = new();

    /// <summary>Identificador que recibirá el próximo pedido; no lo consume.</summary>
    internal string NextOrderId => OrderIdFor(_lastOrderNumber + 1);

    /// <summary>Ejecuta <paramref name="operation"/> con el cerrojo tomado. Nada asíncrono debe ocurrir dentro.</summary>
    internal T Locked<T>(Func<T> operation)
    {
        lock (_gate)
        {
            return operation();
        }
    }

    /// <summary>Registra un pedido recién creado y consume su número.</summary>
    internal void AddOrder(Order order)
    {
        Orders.Add(order.Id, order);
        _lastOrderNumber++;
    }

    private static string OrderIdFor(int number) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"ORD-{number:D4}");
}
