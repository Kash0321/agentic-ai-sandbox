namespace Sandbox.Runner.Scenarios;

/// <summary>Comprobación de un escenario: qué se comprueba, de qué tipo es y el predicado sobre su resultado.</summary>
public sealed record ScenarioCheck(string Description, ScenarioCheckKind Kind, Func<ScenarioOutcome, bool> Holds)
{
    public static ScenarioCheck Safety(string description, Func<ScenarioOutcome, bool> holds) =>
        new(description, ScenarioCheckKind.Safety, holds);

    public static ScenarioCheck Goal(string description, Func<ScenarioOutcome, bool> holds) =>
        new(description, ScenarioCheckKind.Goal, holds);
}
