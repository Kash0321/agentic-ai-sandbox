namespace Sandbox.Runner.Scenarios;

/// <summary>
/// Tipo de comprobación (design D3 de <c>add-scenario-engine</c>): la seguridad la garantiza el sistema, sea cual sea el
/// camino del modelo; el objetivo depende de que el modelo haga bien su trabajo.
/// </summary>
public enum ScenarioCheckKind
{
    Safety,

    Goal,
}
