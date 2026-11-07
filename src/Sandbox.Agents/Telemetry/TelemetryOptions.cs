namespace Sandbox.Agents.Telemetry;

/// <summary>
/// Opciones de la telemetría del agente (sección <c>Telemetry</c>, spec <c>agent-observability</c>). El endpoint de
/// exportación no está aquí: es la opción estándar <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>, que lee el Runner.
/// </summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>
    /// Incluye en los spans de llamada al modelo los mensajes, las definiciones de las tools y sus argumentos y
    /// resultados. Desactivada por defecto (design D5 y ADR 0006).
    /// </summary>
    public bool CaptureContent { get; set; }
}
