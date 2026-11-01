namespace Sandbox.Application.FaultInjection;

/// <summary>
/// Regla del inyector, enlazada desde la configuración. Puede fallar, añadir latencia o las dos cosas; el validador
/// rechaza las que no hacen ninguna.
/// </summary>
public sealed class FaultRule
{
    /// <summary>Operación a la que se aplica (<see cref="FaultOperations"/>) o <c>*</c> para todas.</summary>
    public string Operation { get; set; } = string.Empty;

    public FaultTrigger Trigger { get; set; } = FaultTrigger.None;

    /// <summary>Número de llamadas que fallan con <see cref="FaultTrigger.FirstCalls"/>.</summary>
    public int? Count { get; set; }

    /// <summary>Probabilidad de fallo, de 0 a 1, con <see cref="FaultTrigger.Probability"/>.</summary>
    public double? Probability { get; set; }

    public FaultTiming Timing { get; set; } = FaultTiming.Before;

    /// <summary>Espera añadida antes de ejecutar la operación, falle o no la regla.</summary>
    public TimeSpan Latency { get; set; } = TimeSpan.Zero;
}
