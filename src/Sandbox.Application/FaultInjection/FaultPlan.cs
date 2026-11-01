namespace Sandbox.Application.FaultInjection;

/// <summary>
/// Lo que el inyector decide para una llamada antes de ejecutarla: la latencia total, qué reglas la aportan y, si
/// falla, qué regla lo decide y en qué momento.
/// </summary>
internal sealed record FaultPlan(TimeSpan Latency, IReadOnlyList<int> LatencyRules, int? FailingRule, FaultTiming Timing)
{
    public static FaultPlan None { get; } = new(TimeSpan.Zero, [], null, FaultTiming.Before);
}
