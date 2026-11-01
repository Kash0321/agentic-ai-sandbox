using System.Globalization;
using Microsoft.Extensions.Options;

namespace Sandbox.Application.FaultInjection;

/// <summary>
/// Valida la configuración del inyector al arrancar. Cada mensaje nombra la ruta del ajuste inválido
/// (<c>FaultInjection:Rules:1:Probability</c>) y el motivo.
/// </summary>
public sealed class FaultInjectionOptionsValidator : IValidateOptions<FaultInjectionOptions>
{
    public static readonly TimeSpan MaxLatency = TimeSpan.FromSeconds(30);

    public ValidateOptionsResult Validate(string? name, FaultInjectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        for (var index = 0; index < options.Rules.Count; index++)
        {
            failures.AddRange(Validate(index, options.Rules[index]));
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static IEnumerable<string> Validate(int index, FaultRule rule)
    {
        if (!FaultOperations.Known.Contains(rule.Operation, StringComparer.Ordinal))
        {
            yield return $"{Path(index, nameof(FaultRule.Operation))} debe ser una de: {string.Join(", ", FaultOperations.Known)}.";
        }

        if (rule.Trigger == FaultTrigger.FirstCalls && rule.Count is not >= 1)
        {
            yield return $"{Path(index, nameof(FaultRule.Count))} es obligatorio con {nameof(FaultTrigger.FirstCalls)} y debe ser al menos 1.";
        }

        if (rule.Trigger == FaultTrigger.Probability && rule.Probability is not (>= 0 and <= 1))
        {
            yield return $"{Path(index, nameof(FaultRule.Probability))} es obligatoria con {nameof(FaultTrigger.Probability)} y debe estar entre 0 y 1.";
        }

        if (rule.Latency < TimeSpan.Zero || rule.Latency > MaxLatency)
        {
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"{Path(index, nameof(FaultRule.Latency))} debe estar entre 0 y {MaxLatency.TotalSeconds} segundos.");
        }

        if (rule.Trigger == FaultTrigger.None && rule.Latency == TimeSpan.Zero)
        {
            yield return $"{Path(index, nameof(FaultRule.Trigger))} es {nameof(FaultTrigger.None)} y no hay latencia: la regla no hace nada.";
        }
    }

    private static string Path(int index, string setting) =>
        string.Create(CultureInfo.InvariantCulture, $"{FaultInjectionOptions.SectionName}:Rules:{index}:{setting}");
}
