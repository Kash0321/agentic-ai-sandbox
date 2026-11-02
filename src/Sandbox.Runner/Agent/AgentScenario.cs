using Sandbox.Application.FaultInjection;

namespace Sandbox.Runner.Agent;

/// <summary>Escenario reproducible del agente: cliente de sesión, petición y fallos inducidos que añade.</summary>
public sealed record AgentScenario(string Id, string CustomerId, string Request, Func<IReadOnlyList<FaultRule>> CreateFaults);
