using Sandbox.Application.FaultInjection;

namespace Sandbox.Runner.Agent;

/// <summary>
/// Escenario reproducible del agente: cliente de sesión, petición, fallos inducidos que añade y, si las restringe, las
/// tools que permite. Esas tools reducen la allow-list configurada, nunca la amplían. Los pedidos previos existen antes
/// de atender la petición (spec <c>sales-agent</c>, design D7 de <c>add-tool-approval</c>).
/// </summary>
public sealed record AgentScenario(
    string Id,
    string CustomerId,
    string Request,
    Func<IReadOnlyList<FaultRule>> CreateFaults,
    IReadOnlyList<string>? AllowedTools = null,
    IReadOnlyList<SeedOrder>? SeedOrders = null);
