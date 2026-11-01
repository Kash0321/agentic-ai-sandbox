namespace Sandbox.Application.FaultInjection;

/// <summary>Cuándo falla una regla del inyector.</summary>
public enum FaultTrigger
{
    /// <summary>Nunca falla; la regla solo añade latencia.</summary>
    None,

    /// <summary>Falla en todas las llamadas.</summary>
    Always,

    /// <summary>Falla en las <see cref="FaultRule.Count"/> primeras llamadas y deja pasar las siguientes.</summary>
    FirstCalls,

    /// <summary>Falla con la probabilidad <see cref="FaultRule.Probability"/>, con una secuencia derivada de la semilla.</summary>
    Probability,
}
