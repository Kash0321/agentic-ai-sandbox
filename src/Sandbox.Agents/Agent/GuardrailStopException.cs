namespace Sandbox.Agents.Agent;

/// <summary>
/// Señal interna con la que el cliente de presupuesto corta el bucle de invocación antes de una llamada al modelo
/// (design D6). No sale de <see cref="SalesAgent"/>: se traduce en un resultado con su motivo de parada.
/// </summary>
internal sealed class GuardrailStopException(AgentStopReason reason)
    : Exception($"Ejecución parada por un guardrail: {reason}")
{
    public AgentStopReason Reason { get; } = reason;
}
