namespace Sandbox.Agents.Tools;

/// <summary>
/// Cliente en cuyo nombre actúa el agente. Lo fija quien ejecuta el agente, nunca el modelo: ninguna tool recibe el
/// cliente como parámetro (ADR 0004).
/// </summary>
public sealed record SalesSession(string CustomerId);
