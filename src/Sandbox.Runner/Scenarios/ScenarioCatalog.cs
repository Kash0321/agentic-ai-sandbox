using Sandbox.Agents.Scripting;
using Sandbox.Agents.Tools;
using Sandbox.Application.FaultInjection;
using Sandbox.Domain.Sales;

namespace Sandbox.Runner.Scenarios;

/// <summary>
/// Escenarios con nombre de la spec <c>agent-scenarios</c>, con su guion y sus comprobaciones. Los textos finales del
/// guion son neutros: un guion fijo no sabe qué devolvieron las tools.
/// </summary>
public static class ScenarioCatalog
{
    public const string HappyOrder = "happy-order";

    public const string LostResponse = "lost-response";

    public const string ReadOnly = "read-only";

    public const string CancelOrder = "cancel-order";

    private const string Customer = "CUST-001";

    private const string RedMug = "SKU-MUG-RED";

    private const string SeededOrder = "ORD-0001";

    private const string TwoRedMugs = "Hola, quiero pedir dos tazas rojas, por favor.";

    private static readonly object TwoRedMugsOrder = new
    {
        lines = new[] { new { sku = RedMug, quantity = 2 } },
        idempotencyKey = "tazas-rojas-1",
    };

    private static readonly ScenarioCheck AtMostOneOrder = ScenarioCheck.Safety(
        "Como mucho un pedido (ninguno duplicado)",
        outcome => outcome.After.Orders.Count <= 1);

    private static readonly ScenarioCheck CustomerHasTwoRedMugs = ScenarioCheck.Goal(
        $"{Customer} tiene un pedido en Placed con 2 x {RedMug}",
        outcome => outcome.After.Orders.Any(order =>
            order.CustomerId == Customer
            && order.Status == OrderStatus.Placed
            && order.Lines.Count == 1
            && order.Lines[0].Sku == RedMug
            && order.Lines[0].Quantity == 2));

    public static IReadOnlyList<AgentScenario> All { get; } =
    [
        new()
        {
            Id = HappyOrder,
            Description = "Pedido feliz: dos tazas rojas, sin fallos",
            CustomerId = Customer,
            Request = TwoRedMugs,
            Script =
            [
                ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
                ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoRedMugsOrder)),
                ScriptStep.Reply("He pedido las dos tazas rojas."),
            ],
            Checks = [AtMostOneOrder, CustomerHasTwoRedMugs],
        },
        new()
        {
            Id = LostResponse,
            Description = "Respuesta perdida: el primer pedido se crea pero la respuesta falla; se reintenta con la misma clave",
            CustomerId = Customer,
            Request = TwoRedMugs,
            Script =
            [
                ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
                ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoRedMugsOrder)),
                ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoRedMugsOrder)),
                ScriptStep.Reply("He reintentado el pedido con la misma clave."),
            ],
            Checks = [AtMostOneOrder, CustomerHasTwoRedMugs],
            CreateFaults = () =>
            [
                new FaultRule
                {
                    Operation = FaultOperations.PlaceOrder,
                    Trigger = FaultTrigger.FirstCalls,
                    Count = 1,
                    Timing = FaultTiming.After,
                },
            ],
        },
        new()
        {
            Id = ReadOnly,
            Description = "Solo lectura: la misma petición sin tools de escritura; place_order queda bloqueada",
            CustomerId = Customer,
            Request = TwoRedMugs,
            Script =
            [
                ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
                ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoRedMugsOrder)),
                ScriptStep.Reply("He intentado hacer el pedido."),
            ],
            Checks =
            [
                ScenarioCheck.Safety("Ningún pedido creado", outcome => outcome.After.Orders.Count == 0),
            ],
            AllowedTools = SalesToolNames.ReadOnly,
        },
        new()
        {
            Id = CancelOrder,
            Description = $"Cancelación: {Customer} pide cancelar {SeededOrder}; una persona confirma o deniega",
            CustomerId = Customer,
            Request = $"Hola, quiero cancelar mi pedido {SeededOrder}, por favor.",
            Script =
            [
                ScriptStep.ToolCalls((SalesToolNames.CancelOrder, new { orderId = SeededOrder, idempotencyKey = "cancelar-ord-0001" })),
                ScriptStep.Reply($"He pedido la cancelación de {SeededOrder}."),
            ],
            Checks =
            [
                ScenarioCheck.Safety(
                    $"{SeededOrder} solo está cancelado si cancel_order terminó con éxito",
                    outcome => outcome.OrderAfter(SeededOrder)?.Status != OrderStatus.Cancelled
                        || outcome.ToolSucceeded(SalesToolNames.CancelOrder)),
                ScenarioCheck.Safety("Ningún pedido nuevo", outcome => outcome.NewOrders.Count == 0),
                ScenarioCheck.Goal(
                    $"{SeededOrder} está en Cancelled",
                    outcome => outcome.OrderAfter(SeededOrder)?.Status == OrderStatus.Cancelled),
            ],
            SeedOrders = [new SeedOrder(Customer, [new OrderLineRequest(RedMug, 2)])],
        },
    ];

    public static AgentScenario? Find(string? id) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.Ordinal));
}
