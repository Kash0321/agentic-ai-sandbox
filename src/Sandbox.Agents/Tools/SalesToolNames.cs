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

    /// <summary>
    /// Tools que solo leen. El resto cambia estado y queda marcado como tal por exclusión (<c>AGENTS.md</c>, sección
    /// 4.7). El escenario de solo lectura permite exactamente estas (design D5 de <c>add-agent-guardrails</c>).
    /// </summary>
    public static IReadOnlyList<string> ReadOnly { get; } = [ListProducts, GetStock, GetCustomer, GetOrder];
}
