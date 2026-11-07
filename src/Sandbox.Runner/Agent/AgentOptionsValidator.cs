using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Runner.Scenarios;

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

        failures.AddRange(ValidateModel(options));
        failures.AddRange(ValidateCommand(options));

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> ValidateModel(AgentOptions options)
    {
        var known = options.ModelSource is null
            || options.UsesScriptedModel
            || string.Equals(options.ModelSource.Trim(), AgentOptions.DeepSeekModel, StringComparison.OrdinalIgnoreCase);
        if (!known)
        {
            yield return $"{Path(nameof(AgentOptions.ModelSource))} (--model) debe ser {AgentOptions.DeepSeekModel} o {AgentOptions.ScriptedModel}.";
        }
        else if (options.UsesScriptedModel && options.Scenario is null && !options.ListsScenarios)
        {
            yield return $"{Path(nameof(AgentOptions.ModelSource))} (--model) {AgentOptions.ScriptedModel} necesita un escenario (--scenario): el texto libre y el saludo no tienen guion.";
        }
    }

    private static IEnumerable<string> ValidateCommand(AgentOptions options)
    {
        if (options.Command is null)
        {
            yield break;
        }

        if (!options.ListsScenarios)
        {
            yield return $"Argumento desconocido: {options.Command}. La única suborden es {AgentOptions.ListCommand}.";
        }
        else if (options.RunsAgent)
        {
            yield return $"{AgentOptions.ListCommand} no admite {Path(nameof(AgentOptions.Scenario))} ni {Path(nameof(AgentOptions.Prompt))}.";
        }
    }

    private static string Path(string property) => $"{AgentOptions.SectionName}:{property}";
}
