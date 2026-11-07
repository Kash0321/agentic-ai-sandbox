using Sandbox.Runner.Scenarios;

namespace Sandbox.Scenarios.Tests.Scenarios;

/// <summary>Spec <c>agent-scenarios</c>, "Definición de un escenario" y "Escenarios con nombre".</summary>
public sealed class ScenarioCatalogTests
{
    [Fact]
    public void All_Always_ContainsTheFourNamedScenarios()
    {
        Assert.Equal(["happy-order", "lost-response", "read-only", "cancel-order"], ScenarioCatalog.All.Select(scenario => scenario.Id));
    }

    [Fact]
    public void All_Always_HasUniqueIdentifiers()
    {
        Assert.Equal(ScenarioCatalog.All.Count, ScenarioCatalog.All.Select(scenario => scenario.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void All_Always_HasDescriptionAndAScriptThatEndsWithAReply()
    {
        Assert.All(ScenarioCatalog.All, scenario =>
        {
            Assert.False(string.IsNullOrWhiteSpace(scenario.Description), scenario.Id);
            Assert.NotEmpty(scenario.Script);
            var last = scenario.Script[^1];
            Assert.Empty(last.Calls);
            Assert.False(string.IsNullOrWhiteSpace(last.Text), scenario.Id);
        });
    }

    [Fact]
    public void All_Always_HasAtLeastOneSafetyCheckIncludingTheCommonOnes()
    {
        Assert.All(ScenarioCatalog.All, scenario =>
            Assert.Contains(CommonChecks.All.Concat(scenario.Checks), check => check.Kind == ScenarioCheckKind.Safety));
    }

    [Fact]
    public void All_Always_HasSingleLineDescriptions()
    {
        // list escribe una línea por escenario, y la CI toma su primera columna.
        Assert.All(ScenarioCatalog.All, scenario =>
            Assert.DoesNotContain(scenario.Description, character => char.IsControl(character)));
    }
}
