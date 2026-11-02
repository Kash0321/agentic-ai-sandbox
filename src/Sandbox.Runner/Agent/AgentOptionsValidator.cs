using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;

namespace Sandbox.Runner.Agent;

/// <summary>Valida al arrancar los argumentos de ejecución del agente; los mensajes nombran la opción.</summary>
public sealed class AgentOptionsValidator : IValidateOptions<AgentOptions>
{
    public ValidateOptionsResult Validate(string? name, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.Scenario is not null && options.Prompt is not null)
        {
            failures.Add($"No se pueden indicar {Path(nameof(AgentOptions.Scenario))} y {Path(nameof(AgentOptions.Prompt))} a la vez.");
        }

        if (options.Scenario is not null && ScenarioCatalog.Find(options.Scenario) is null)
        {
            var known = string.Join(", ", ScenarioCatalog.All.Select(scenario => scenario.Id));
            failures.Add($"{Path(nameof(AgentOptions.Scenario))} debe ser uno de: {known}.");
        }

        if (options.Prompt is not null && string.IsNullOrWhiteSpace(options.Prompt))
        {
            failures.Add($"{Path(nameof(AgentOptions.Prompt))} no puede estar vacío.");
        }

        if (options.Prompt is { Length: > SalesAgent.MaxRequestLength })
        {
            failures.Add($"{Path(nameof(AgentOptions.Prompt))} admite como máximo {SalesAgent.MaxRequestLength} caracteres.");
        }

        if (options.CustomerId is not null && string.IsNullOrWhiteSpace(options.CustomerId))
        {
            failures.Add($"{Path(nameof(AgentOptions.CustomerId))} no puede estar vacío.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string Path(string property) => $"{AgentOptions.SectionName}:{property}";
}
