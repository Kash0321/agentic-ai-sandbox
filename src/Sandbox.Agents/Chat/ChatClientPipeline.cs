using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Sandbox.Agents.Telemetry;

namespace Sandbox.Agents.Chat;

/// <summary>
/// Decoración común del cliente de un proveedor, sea DeepSeek o el modelo con guion (design D1 de
/// <c>add-scenario-engine</c>): así una ejecución con guion se registra y se traza igual que una real.
/// </summary>
public static class ChatClientPipeline
{
    /// <param name="providerClient">Cliente del proveedor, sin decorar.</param>
    /// <param name="loggerFactory">Para el registro de cada llamada.</param>
    /// <param name="telemetry">Opciones de telemetría; sin ellas, los spans no llevan contenido.</param>
    public static ChatClientBuilder CreateBuilder(
        IChatClient providerClient,
        ILoggerFactory loggerFactory,
        TelemetryOptions? telemetry = null)
    {
        ArgumentNullException.ThrowIfNull(providerClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var captureContent = telemetry?.CaptureContent ?? false;
        return providerClient.AsBuilder()
            // Por fuera del traductor: el span copia el mensaje de la excepción en su estado, y el del proveedor puede
            // repetir parte de la clave (design D2 de add-agent-observability). Sin ILoggerFactory, para que ningún
            // contenido llegue a los registros de consola aunque se capture en los spans.
            .UseOpenTelemetry(
                sourceName: AgentTelemetry.SourceName,
                configure: client => client.EnableSensitiveData = captureContent)
            .Use(inner => new ProviderErrorTranslatingChatClient(inner))
            .Use(inner => new UsageLoggingChatClient(inner, loggerFactory.CreateLogger<UsageLoggingChatClient>()));
    }
}
