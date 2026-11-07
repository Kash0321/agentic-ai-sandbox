using Sandbox.Agents.Scripting;
using Sandbox.Application.FaultInjection;

namespace Sandbox.Runner.Scenarios;

/// <summary>
/// Escenario reproducible y verificable del agente (spec <c>agent-scenarios</c>; design D2 y D3 de
/// <c>add-scenario-engine</c>):
/// <list type="bullet">
/// <item>preparación: cliente de sesión, petición, fallos inducidos que añade, tools a las que restringe (reducen la
/// allow-list configurada, nunca la amplían) y pedidos previos, que existen antes de atender la petición;</item>
/// <item>guion: lo que respondería un modelo concreto, en orden y sin reaccionar a los resultados;</item>
/// <item>comprobaciones propias de seguridad y de objetivo; las comunes las añade el motor.</item>
/// </list>
/// </summary>
public sealed record AgentScenario
{
    public required string Id { get; init; }

    public required string Description { get; init; }

    public required string CustomerId { get; init; }

    public required string Request { get; init; }

    public required IReadOnlyList<ScriptStep> Script { get; init; }

    public IReadOnlyList<ScenarioCheck> Checks { get; init; } = [];

    public Func<IReadOnlyList<FaultRule>> CreateFaults { get; init; } = () => [];

    public IReadOnlyList<string>? AllowedTools { get; init; }

    public IReadOnlyList<SeedOrder> SeedOrders { get; init; } = [];
}
