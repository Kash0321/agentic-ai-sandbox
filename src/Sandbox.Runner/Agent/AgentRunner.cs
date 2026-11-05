using System.Globalization;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Application.Sales;

namespace Sandbox.Runner.Agent;

/// <summary>
/// Ejecuta el agente con el escenario o el texto indicados y escribe por la salida estándar la respuesta final y un
/// resumen. Los textos intermedios y las trazas van al registro, por la salida de errores. Devuelve el resultado para
/// que el host elija el código de salida. Antes crea los pedidos previos del escenario con <see cref="SalesCommands"/>,
/// sin el inyector de fallos, para no consumir sus reglas (design D7 de <c>add-tool-approval</c>).
/// </summary>
internal sealed class AgentRunner(SalesAgent agent, SalesCommands commands, IOptions<AgentOptions> options)
{
    public async Task<AgentRunResult> RunAsync(TextWriter output, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var scenario = ScenarioCatalog.Find(settings.Scenario);
        await SeedOrdersAsync(scenario, cancellationToken);

        var request = scenario?.Request
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
