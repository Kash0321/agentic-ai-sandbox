using Microsoft.Extensions.Options;
using Sandbox.Agents.Tools;

namespace Sandbox.Agents.Guardrails;

/// <summary>
/// Valida <see cref="GuardrailOptions"/> al arrancar; los mensajes nombran el ajuste. Una lista de tools vacía es
/// inválida (<i>fail closed</i>): un agente sin tools no es lo que nadie quiere configurar a propósito.
/// </summary>
public sealed class GuardrailOptionsValidator : IValidateOptions<GuardrailOptions>
{
    public ValidateOptionsResult Validate(string? name, GuardrailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        RequireAtLeastOne(options.MaxModelCalls, nameof(GuardrailOptions.MaxModelCalls), failures);
        RequireAtLeastOne(options.MaxConsecutiveToolErrors, nameof(GuardrailOptions.MaxConsecutiveToolErrors), failures);
        RequireAtLeastOne(options.TokenBudget, nameof(GuardrailOptions.TokenBudget), failures);

        if (options.AllowedTools is { } allowed)
        {
            var path = Path(nameof(GuardrailOptions.AllowedTools));
            if (allowed.Count == 0)
            {
                failures.Add($"{path} no puede quedar vacía.");
            }

            var unknown = allowed.Where(tool => !SalesToolNames.All.Contains(tool, StringComparer.Ordinal)).ToList();
            if (unknown.Count > 0)
            {
                failures.Add($"{path} contiene tools que no existen: {string.Join(", ", unknown)}. Las tools son: {string.Join(", ", SalesToolNames.All)}.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequireAtLeastOne(int value, string property, List<string> failures)
    {
        if (value < 1)
        {
            failures.Add($"{Path(property)} debe ser 1 o mayor.");
        }
    }

    private static string Path(string property) => $"{GuardrailOptions.SectionName}:{property}";
}
