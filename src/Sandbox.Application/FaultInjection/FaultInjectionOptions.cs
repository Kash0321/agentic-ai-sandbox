namespace Sandbox.Application.FaultInjection;

/// <summary>
/// Configuración del inyector de fallos (sección <c>FaultInjection</c>). Sin reglas, el inyector no hace nada.
/// </summary>
public sealed class FaultInjectionOptions
{
    public const string SectionName = "FaultInjection";

    /// <summary>Semilla base: la regla de índice <c>i</c> usa <c>Seed + i</c>.</summary>
    public int Seed { get; set; }

    /// <summary>Reglas en el orden en que se evalúan.</summary>
    public IList<FaultRule> Rules { get; } = [];
}
