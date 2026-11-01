namespace Sandbox.Application.FaultInjection;

/// <summary>Nombres de las operaciones a las que se pueden aplicar reglas del inyector.</summary>
public static class FaultOperations
{
    public const string PlaceOrder = nameof(PlaceOrder);
    public const string CancelOrder = nameof(CancelOrder);
    public const string ShipOrder = nameof(ShipOrder);
    public const string GetOrder = nameof(GetOrder);
    public const string GetCustomer = nameof(GetCustomer);
    public const string GetStock = nameof(GetStock);

    /// <summary>Comodín: la regla se aplica a todas las operaciones.</summary>
    public const string All = "*";

    public static IReadOnlyList<string> Known { get; } =
        [PlaceOrder, CancelOrder, ShipOrder, GetOrder, GetCustomer, GetStock, All];
}
