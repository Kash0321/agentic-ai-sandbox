namespace Sandbox.Agents.Agent;

/// <summary>Por qué terminó una ejecución del agente (spec <c>agent-guardrails</c>, "Motivo de parada").</summary>
public enum AgentStopReason
{
    /// <summary>El modelo respondió con texto sin pedir más tools.</summary>
    Completed,

    /// <summary>Se alcanzó el máximo de llamadas al modelo.</summary>
    ModelCallLimit,

    /// <summary>Demasiadas invocaciones de tools seguidas terminaron con error.</summary>
    ToolErrorLimit,

    /// <summary>Los tokens acumulados alcanzaron el presupuesto.</summary>
    TokenBudget,
}
