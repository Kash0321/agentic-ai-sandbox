using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sandbox.Agents.Approval;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Runner;
using Sandbox.Runner.Scenarios;
using Sandbox.Scenarios.Tests.Runner;

namespace Sandbox.Scenarios.Tests.Scenarios;

/// <summary>
/// Matriz de escenarios: cada escenario del catálogo, con su guion, sin red ni clave y con un aprobador que aprueba,
/// termina seguro y con su objetivo cumplido o sin objetivo (spec <c>agent-scenarios</c>, "Matriz de escenarios en los
/// tests"; design D9 de <c>add-scenario-engine</c>). Un escenario nuevo queda cubierto sin escribir un test propio.
/// </summary>
[Collection(nameof(RunnerHostSerialGroup))]
public sealed class ScenarioMatrixTests
{
    public static TheoryData<string> ScenarioIds => [.. ScenarioCatalog.All.Select(scenario => scenario.Id)];

    [Theory]
    [MemberData(nameof(ScenarioIds))]
    public async Task RunAsync_WithItsScriptAndAnApprovingApprover_IsSafeAndMeetsItsGoal(string scenarioId)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        using var logs = new RecordingLoggerProvider();
        var builder = RunnerHost.CreateBuilder(["--scenario", scenarioId, "--model", "scripted"], TemporarySecretsDirectory.Missing());
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton<IToolApprover>(new ScriptedToolApprover([.. Enumerable.Repeat(true, 10)]));

        var exitCode = await RunnerHost.RunAsync(builder, output, error, TestContext.Current.CancellationToken);

        var text = output.ToString();
        Assert.True(exitCode == ExitCodes.Success, $"{scenarioId}: código {exitCode}. {error}");
        Assert.DoesNotContain("[FALLO]", text, StringComparison.Ordinal);
        Assert.True(
            text.Contains($"Veredicto: {ScenarioVerdict.SafeGoalMet}", StringComparison.Ordinal)
                || text.Contains($"Veredicto: {ScenarioVerdict.SafeWithoutGoal}", StringComparison.Ordinal),
            $"{scenarioId}: {text}");
    }
}
