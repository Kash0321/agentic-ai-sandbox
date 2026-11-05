using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Sandbox.Application.Auditing;
using Sandbox.Application.Tests.Fakes;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Application.Tests.Sales;

/// <summary>
/// Commands y queries sobre un estado propio del test, con un reloj falso. Por defecto usa el catálogo semilla; los
/// tests que necesitan otros datos construyen su estado con <see cref="StateWith"/>.
/// </summary>
internal sealed class SalesHarness
{
    public const string ActiveCustomerId = "CUST-A";
    public const string BlockedCustomerId = "CUST-B";

    public static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static readonly string[] KnownCustomers = [ActiveCustomerId, BlockedCustomerId, "CUST-001", "CUST-002", "CUST-003"];

    // Busca propietarios sin dejar accesos denegados en la auditoría que comprueban los tests.
    private readonly SalesQueries _ownerLookup;

    public SalesHarness(SalesState? state = null)
    {
        State = state ?? SeedCatalog.CreateState();
        var audit = new AuditLog(AuditLogger);
        Commands = new SalesCommands(State, Clock, audit);
        Queries = new SalesQueries(State, audit);
        _ownerLookup = new SalesQueries(State, new AuditLog(NullLogger.Instance));
    }

    public FakeTimeProvider Clock { get; } = new(Now);

    /// <summary>Eventos de auditoría de los commands y queries del arnés.</summary>
    public RecordingLogger<AuditLog> AuditLogger { get; } = new();

    public SalesState State { get; }

    public ISalesCommands Commands { get; }

    public ISalesQueries Queries { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Estado con un cliente activo, uno bloqueado y los productos indicados.</summary>
    public static SalesState StateWith(params StockItem[] products) =>
        new(
            [
                new Customer(ActiveCustomerId, "Cliente activo", CustomerStatus.Active),
                new Customer(BlockedCustomerId, "Cliente bloqueado", CustomerStatus.Blocked),
            ],
            products);

    public static StockItem Product(string sku, decimal unitPrice = 10.00m, int onHand = 20) =>
        new(sku, $"Producto {sku}", unitPrice, onHand);

    public static OrderLineRequest Line(string sku, int quantity) => new(sku, quantity);

    public Task<Result<OrderView>> PlaceAsync(string key, string customerId, params OrderLineRequest[] lines) =>
        Commands.PlaceOrderAsync(new PlaceOrderCommand(key, customerId, lines), Token);

    /// <summary>Cancela en nombre de <paramref name="customerId"/> o, si no se indica, del propietario del pedido.</summary>
    public async Task<Result<OrderView>> CancelAsync(string key, string orderId, string? customerId = null) =>
        await Commands.CancelOrderAsync(
            new CancelOrderCommand(key, orderId, customerId ?? await OwnerOfAsync(orderId)),
            Token);

    public Task<Result<OrderView>> ShipAsync(string key, string orderId) =>
        Commands.ShipOrderAsync(new ShipOrderCommand(key, orderId), Token);

    public async Task<OrderView> PlacedOrderAsync(string key, string customerId, params OrderLineRequest[] lines)
    {
        var result = await PlaceAsync(key, customerId, lines);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value;
    }

    public async Task<int> AvailableAsync(string sku)
    {
        var result = await Queries.GetStockAsync(new GetStockQuery(sku), Token);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value.Available;
    }

    public async Task<OrderView> OrderAsync(string orderId)
    {
        var result = await Queries.GetOrderAsync(new GetOrderQuery(orderId, await OwnerOfAsync(orderId)), Token);
        Assert.True(result.IsSuccess, result.Error?.Code);
        return result.Value;
    }

    public async Task<bool> OrderExistsAsync(string orderId) =>
        (await Queries.GetOrderAsync(new GetOrderQuery(orderId, await OwnerOfAsync(orderId)), Token)).IsSuccess;

    /// <summary>
    /// Propietario de un pedido, buscado entre los clientes de los estados de prueba: un cliente solo ve sus pedidos,
    /// y los tests que no tratan de la autorización leen cada pedido como su propietario. Si no existe, el cliente activo.
    /// </summary>
    private async Task<string> OwnerOfAsync(string orderId)
    {
        foreach (var customerId in KnownCustomers)
        {
            if ((await _ownerLookup.GetOrderAsync(new GetOrderQuery(orderId, customerId), Token)).IsSuccess)
            {
                return customerId;
            }
        }

        return ActiveCustomerId;
    }
}
