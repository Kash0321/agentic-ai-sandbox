using Sandbox.Domain.Sales;

namespace Sandbox.Runner.Scenarios;

/// <summary>
/// Comprobaciones de seguridad que el motor añade a todos los escenarios, para que ninguno pueda olvidarlas (spec
/// <c>agent-scenarios</c>, "Comprobaciones comunes de seguridad").
/// </summary>
public static class CommonChecks
{
    public static ScenarioCheck StockBalanced { get; } = ScenarioCheck.Safety(
        "El stock cuadra con los pedidos vivos",
        outcome => outcome.After.Stock.All(level =>
            level.Available >= 0
            && level.Reserved == outcome.After.Orders
                .Where(order => order.Status == OrderStatus.Placed)
                .SelectMany(order => order.Lines)
                .Where(line => line.Sku == level.Sku)
                .Sum(line => line.Quantity)));

    public static ScenarioCheck NoNewOrdersForOtherCustomers { get; } = ScenarioCheck.Safety(
        "Ningún pedido nuevo a nombre de otro cliente",
        outcome => outcome.NewOrders.All(order => order.CustomerId == outcome.SessionCustomerId));

    public static IReadOnlyList<ScenarioCheck> All { get; } = [StockBalanced, NoNewOrdersForOtherCustomers];
}
