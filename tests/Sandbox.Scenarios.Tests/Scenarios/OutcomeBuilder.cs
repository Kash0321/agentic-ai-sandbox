using Sandbox.Agents.Agent;
using Sandbox.Application.Inspection;
using Sandbox.Application.Sales;
using Sandbox.Domain.Sales;
using Sandbox.Runner.Scenarios;

namespace Sandbox.Scenarios.Tests.Scenarios;

/// <summary>Resultados de escenario construidos a mano, para probar las comprobaciones sin ejecutar el agente.</summary>
internal static class OutcomeBuilder
{
    public const string RedMug = "SKU-MUG-RED";

    public static OrderView Order(string orderId, string customerId, OrderStatus status, int redMugs = 2) =>
        new(
            orderId,
            customerId,
            status,
            [new OrderLineView(RedMug, redMugs, 12.50m, 12.50m * redMugs)],
            12.50m * redMugs,
            "EUR",
            DateTimeOffset.UnixEpoch);

    /// <summary>Stock de tazas rojas (20 existencias) con lo reservado indicado.</summary>
    public static StockLevel RedMugs(int reserved) => new(RedMug, 20, reserved, 20 - reserved);

    public static SalesSnapshot Snapshot(IReadOnlyList<OrderView> orders, params StockLevel[] stock) => new(orders, stock);

    public static AgentRunResult Run(params (string Name, string Outcome)[] tools) =>
        new("respuesta", 2, [.. tools.Select(tool => new ToolInvocation(tool.Name, tool.Outcome))], 200, 20, AgentStopReason.Completed);

    public static ScenarioOutcome Outcome(
        SalesSnapshot after,
        SalesSnapshot? before = null,
        AgentRunResult? run = null,
        string sessionCustomerId = "CUST-001") =>
        new(before ?? Snapshot([], RedMugs(0)), after, run ?? Run(), sessionCustomerId);

    public static bool Passes(ScenarioCheck check, ScenarioOutcome outcome) => check.Holds(outcome);
}
