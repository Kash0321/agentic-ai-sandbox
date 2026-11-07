using System.Diagnostics;

namespace Sandbox.Agents.Telemetry;

/// <summary>
/// Fuente única de las trazas y métricas del agente y del modelo (design D1 de <c>add-agent-observability</c>). Los
/// atributos con prefijo <c>gen_ai.</c> siguen las convenciones semánticas de OpenTelemetry para IA generativa; los que
/// no tienen equivalente usan el prefijo propio <c>sandbox.</c> (D3).
/// </summary>
public static class AgentTelemetry
{
    /// <summary>Nombre de la fuente de trazas y del medidor que registra el Runner.</summary>
    public const string SourceName = "Sandbox.Agents";

    /// <summary>
    /// Nombre del span raíz de una ejecución. No es solo una convención: bajo un span con este nombre,
    /// <c>FunctionInvokingChatClient</c> no abre un <c>orchestrate_tools</c> por vuelta (ajuste del apply, A2b).
    /// </summary>
    public const string InvokeAgentSpanName = "invoke_agent sales-agent";

    public const string AgentName = "sales-agent";

    public const string RunIdTag = "sandbox.run.id";

    public const string StopReasonTag = "sandbox.stop_reason";

    public const string ModelCallsTag = "sandbox.model_calls";

    public const string ToolOutcomeTag = "sandbox.tool.outcome";

    public const string ApprovalDecisionTag = "sandbox.approval.decision";

    public const string OperationNameTag = "gen_ai.operation.name";

    public const string AgentNameTag = "gen_ai.agent.name";

    public const string ToolNameTag = "gen_ai.tool.name";

    public const string InputTokensTag = "gen_ai.usage.input_tokens";

    public const string OutputTokensTag = "gen_ai.usage.output_tokens";

    /// <summary>
    /// Fuente de los spans propios del agente. Es estática como recomienda .NET para <see cref="ActivitySource"/>: no
    /// guarda estado de ninguna ejecución, y sin oyentes no crea spans.
    /// </summary>
    internal static ActivitySource Source { get; } = new(SourceName);
}
