using System.Globalization;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;

namespace Sandbox.Runner.Agent;

/// <summary>
/// Ejecuta el agente con el escenario o el texto indicados y escribe por la salida estándar la respuesta final y un
/// resumen. Los textos intermedios y las trazas van al registro, por la salida de errores. Devuelve el resultado para
/// que el host elija el código de salida.
/// </summary>
internal sealed class AgentRunner(SalesAgent agent, IOptions<AgentOptions> options)
{
    public async Task<AgentRunResult> RunAsync(TextWriter output, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var request = ScenarioCatalog.Find(settings.Scenario)?.Request
            ?? settings.Prompt
            ?? throw new InvalidOperationException("No hay escenario ni texto que ejecutar.");

        var result = await agent.RunAsync(request, cancellationToken);

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

        return result;
    }
}
