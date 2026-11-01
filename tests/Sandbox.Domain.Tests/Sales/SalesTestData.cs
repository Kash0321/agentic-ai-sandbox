using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Domain.Tests.Sales;

/// <summary>Datos de prueba construidos a medida en cada test, independientes del catálogo semilla.</summary>
internal static class SalesTestData
{
    public const string OrderId = "ORD-0001";

    public static readonly DateTimeOffset PlacedAt = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    public static Customer ActiveCustomer() => new("CUST-T1", "Cliente de prueba", CustomerStatus.Active);

    public static Customer BlockedCustomer() => new("CUST-T2", "Cliente bloqueado de prueba", CustomerStatus.Blocked);

    public static StockItem Product(string sku, decimal unitPrice = 10.00m, int onHand = 20) =>
        new(sku, $"Producto {sku}", unitPrice, onHand);

    public static Dictionary<string, StockItem> Stock(params StockItem[] items) =>
        items.ToDictionary(item => item.Sku, StringComparer.Ordinal);

    public static OrderLineRequest Line(string sku, int quantity) => new(sku, quantity);

    public static Result<Order> Create(
        Customer customer,
        IReadOnlyDictionary<string, StockItem> stock,
        params OrderLineRequest[] lines) =>
        Order.Create(OrderId, customer, lines, stock, PlacedAt);

    /// <summary>Crea un pedido que el test da por bueno; si falla, el test falla con el código del error.</summary>
    public static Order PlacedOrder(IReadOnlyDictionary<string, StockItem> stock, params OrderLineRequest[] lines)
    {
        var result = Create(ActiveCustomer(), stock, lines);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value;
    }
}
