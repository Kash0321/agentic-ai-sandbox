namespace Sandbox.Agents.Agent;

/// <summary>
/// Resultado de una ejecución del agente: la respuesta final (el texto del último mensaje del modelo), cuántas veces se
/// llamó al modelo, las tools invocadas en orden y los tokens acumulados de todas las llamadas.
/// </summary>
public sealed record AgentRunResult(
    string FinalAnswer,
    int ModelCalls,
    IReadOnlyList<ToolInvocation> ToolInvocations,
    long InputTokens,
    long OutputTokens);
