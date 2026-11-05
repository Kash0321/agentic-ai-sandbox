using Sandbox.Application.FaultInjection;

namespace Sandbox.Runner.Agent;

/// <summary>
/// Escenario reproducible del agente: cliente de sesión, petición, fallos inducidos que añade y, si las restringe, las
/// tools que permite. Esas tools reducen la allow-list configurada, nunca la amplían.
/// </summary>
public sealed record AgentScenario(
    string Id,
    string CustomerId,
    string Request,
    Func<IReadOnlyList<FaultRule>> CreateFaults,
    IReadOnlyList<string>? AllowedTools = null);
