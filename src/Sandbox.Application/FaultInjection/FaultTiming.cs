namespace Sandbox.Application.FaultInjection;

/// <summary>Momento del fallo inducido respecto a la operación.</summary>
public enum FaultTiming
{
    /// <summary>Falla sin ejecutar la operación: el estado no cambia.</summary>
    Before,

    /// <summary>Ejecuta la operación, conserva sus efectos y devuelve el error: simula una respuesta perdida.</summary>
    After,
}
