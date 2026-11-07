namespace Sandbox.Runner.Scenarios;

/// <summary>Resultado de evaluar una comprobación.</summary>
public sealed record CheckResult(ScenarioCheck Check, bool Passed);
