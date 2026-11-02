using Sandbox.Application.FaultInjection;

namespace Sandbox.Runner.Agent;

/// <summary>Escenarios con nombre de la spec <c>sales-agent</c>. La matriz completa llega en la fase 6.</summary>
public static class ScenarioCatalog
{
    public const string HappyOrder = "happy-order";

    public const string LostResponse = "lost-response";

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
    ];

    public static AgentScenario? Find(string? id) =>
        All.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.Ordinal));
}
