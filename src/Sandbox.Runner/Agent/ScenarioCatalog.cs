using Sandbox.Agents.Tools;
using Sandbox.Application.FaultInjection;

namespace Sandbox.Runner.Agent;

/// <summary>Escenarios con nombre de la spec <c>sales-agent</c>. La matriz completa llega en la fase 6.</summary>
public static class ScenarioCatalog
{
    public const string HappyOrder = "happy-order";

    public const string LostResponse = "lost-response";

    public const string ReadOnly = "read-only";

    private const string TwoRedMugs = "Hola, quiero pedir dos tazas rojas, por favor.";

    public static IReadOnlyList<AgentScenario> All { get; } =
    [
        new(HappyOrder, "CUST-001", TwoRedMugs, () => []),
        new(
            LostResponse,
            "CUST-001",
            TwoRedMugs,
            () =>
            [
                new FaultRule
                {
                    Operation = FaultOperations.PlaceOrder,
                    Trigger = FaultTrigger.FirstCalls,
                    Count = 1,
                    Timing = FaultTiming.After,
                },
            ]),
        new(ReadOnly, "CUST-001", TwoRedMugs, () => [], SalesToolNames.ReadOnly),
    ];

    public static AgentScenario? Find(string? id) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.Ordinal));
}
