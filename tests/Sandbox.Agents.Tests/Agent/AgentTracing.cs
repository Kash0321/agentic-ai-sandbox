namespace Sandbox.Agents.Tests.Agent;

/// <summary>Cómo envuelve <see cref="AgentHarness"/> el modelo para las pruebas de telemetría.</summary>
internal enum AgentTracing
{
    /// <summary>Modelo sin envolver, como en los tests que no miran la telemetría.</summary>
    None,

    /// <summary>Cliente de OpenTelemetry sin captura de contenido.</summary>
    WithoutContent,

    /// <summary>Cliente de OpenTelemetry con captura de contenido.</summary>
    WithContent,
}
