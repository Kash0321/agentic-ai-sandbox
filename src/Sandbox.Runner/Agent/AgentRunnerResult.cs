using Sandbox.Agents.Agent;
using Sandbox.Runner.Scenarios;

namespace Sandbox.Runner.Agent;

/// <summary>Resultado de una ejecución del Runner: el del agente y, si se ejecutó un escenario, su veredicto.</summary>
internal sealed record AgentRunnerResult(AgentRunResult Run, ScenarioVerdict? Verdict);
