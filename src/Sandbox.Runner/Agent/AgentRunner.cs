using System.Globalization;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Tools;
using Sandbox.Application.Inspection;
using Sandbox.Application.Sales;
using Sandbox.Runner.Scenarios;

namespace Sandbox.Runner.Agent;

/// <summary>
/// Ejecuta el agente con el escenario o el texto indicados y escribe por la salida estándar la respuesta final y un
/// resumen. Los textos intermedios y las trazas van al registro, por la salida de errores. Antes crea los pedidos
/// previos del escenario con <see cref="SalesCommands"/>, sin el inyector de fallos, para no consumir sus reglas
/// (design D7 de <c>add-tool-approval</c>). Con un escenario, evalúa después sus comprobaciones sobre la lectura de
/// inspección y escribe el veredicto (design D3, D5 y D7 de <c>add-scenario-engine</c>).
/// </summary>
internal sealed class AgentRunner(
    SalesAgent agent,
    SalesCommands commands,
    SalesInspector inspector,
    SalesSession session,
    IOptions<AgentOptions> options)
{
    public const string ScriptedModelNotice = "MODELO GUIONIZADO: respuestas fijas del escenario, no es un LLM";

    public async Task<AgentRunnerResult> RunAsync(TextWriter output, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var scenario = ScenarioCatalog.Find(settings.Scenario);
        if (settings.UsesScriptedModel)
        {
            await output.WriteLineAsync(ScriptedModelNotice);
        }

        await SeedOrdersAsync(scenario, cancellationToken);
        var before = await inspector.SnapshotAsync(cancellationToken);

        var request = scenario?.Request
            ?? settings.Prompt
            ?? throw new InvalidOperationException("No hay escenario ni texto que ejecutar.");

        var result = await agent.RunAsync(request, cancellationToken);
        await WriteSummaryAsync(output, result);

        if (scenario is null)
        {
            return new AgentRunnerResult(result, null);
        }

        var after = await inspector.SnapshotAsync(cancellationToken);
        var verdict = ScenarioVerdict.Evaluate(scenario, new ScenarioOutcome(before, after, result, session.CustomerId));
        await WriteVerdictAsync(output, scenario, verdict);
        return new AgentRunnerResult(result, verdict);
    }

    private static async Task WriteSummaryAsync(TextWriter output, AgentRunResult result)
    {
        var tools = result.ToolInvocations.Count == 0
            ? "ninguna"
            : string.Join(", ", result.ToolInvocations.Select(invocation => $"{invocation.Name} ({invocation.Outcome})"));

        await output.WriteLineAsync($"Respuesta: {result.FinalAnswer}");
        await output.WriteLineAsync($"Motivo de parada: {result.StopReason}");
        await output.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"Llamadas al modelo: {result.ModelCalls}"));
        await output.WriteLineAsync($"Tools: {tools}");
        await output.WriteLineAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"Tokens: {result.InputTokens} de entrada y {result.OutputTokens} de salida"));
    }

    private static async Task WriteVerdictAsync(TextWriter output, AgentScenario scenario, ScenarioVerdict verdict)
    {
        await output.WriteLineAsync();
        await output.WriteLineAsync($"Comprobaciones del escenario {scenario.Id}");
        await WriteGroupAsync(output, "Seguridad", verdict.Safety);
        await WriteGroupAsync(output, "Objetivo", verdict.Goal);
        await output.WriteLineAsync($"Veredicto: {verdict.Summary}");
    }

    private static async Task WriteGroupAsync(TextWriter output, string title, IReadOnlyList<CheckResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        await output.WriteLineAsync($"  {title}");
        foreach (var result in results)
        {
            await output.WriteLineAsync($"    {(result.Passed ? "[OK]" : "[FALLO]")} {result.Check.Description}");
        }
    }

    // Un pedido previo que falla es un error del catálogo de escenarios, no del modelo: el host lo trata como inesperado.
    private async Task SeedOrdersAsync(AgentScenario? scenario, CancellationToken cancellationToken)
    {
        var seeds = scenario?.SeedOrders ?? [];
        for (var index = 0; index < seeds.Count; index++)
        {
            var seed = seeds[index];
            var key = string.Create(CultureInfo.InvariantCulture, $"seed-{index + 1}");
            var placed = await commands.PlaceOrderAsync(new PlaceOrderCommand(key, seed.CustomerId, seed.Lines), cancellationToken);
            if (!placed.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"El pedido previo {index + 1} del escenario {scenario?.Id} no se pudo crear: {placed.Error.Code}.");
            }
        }
    }
}
