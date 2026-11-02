namespace Sandbox.Agents.Tools;

/// <summary>Nombres estables de las tools que ve el modelo (spec <c>agent-tools</c>). No cambiarlos sin actualizar tests y prompt.</summary>
public static class SalesToolNames
{
    public const string ListProducts = "list_products";
    public const string GetStock = "get_stock";
    public const string GetCustomer = "get_customer";
    public const string GetOrder = "get_order";
    public const string PlaceOrder = "place_order";
    public const string CancelOrder = "cancel_order";

    public static IReadOnlyList<string> All { get; } =
        [ListProducts, GetStock, GetCustomer, GetOrder, PlaceOrder, CancelOrder];
}
