namespace Sandbox.Agents.Agent;

/// <summary>
/// Resultado de una ejecución del agente: la respuesta final (el texto del último mensaje del modelo o, si la paró un
/// guardrail, el texto fijo del sistema), cuántas veces se llamó al modelo, las tools pedidas en orden, los tokens
/// acumulados de todas las llamadas y el motivo de parada.
/// </summary>
public sealed record AgentRunResult(
    string FinalAnswer,
    int ModelCalls,
    IReadOnlyList<ToolInvocation> ToolInvocations,
    long InputTokens,
    long OutputTokens,
    AgentStopReason StopReason);
