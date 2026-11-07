using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sandbox.Agents.Telemetry;

namespace Sandbox.Runner.Telemetry;

/// <summary>
/// Registro de la telemetría del Runner (spec <c>agent-observability</c>, design D6 a D8 de
/// <c>add-agent-observability</c>). Sin endpoint no se registra nada de OpenTelemetry; con uno inválido tampoco, y la
/// validación al arrancar termina con el código 2.
/// </summary>
public static class RunnerTelemetry
{
    /// <summary>Nombre del servicio en la telemetría.</summary>
    public const string ServiceName = "sandbox-runner";

    public static void AddRunnerTelemetry(this HostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<OtlpExportOptions>()
            .Configure<IConfiguration>((options, configuration) => options.Endpoint = configuration[OtlpExportOptions.EndpointKey])
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<OtlpExportOptions>, OtlpExportOptionsValidator>();

        if (OtlpExportOptions.Parse(builder.Configuration[OtlpExportOptions.EndpointKey]) is null)
        {
            return;
        }

        builder.Services.AddOpenTelemetry()
            // Sin identificador de instancia: cada ejecución es un proceso nuevo y el panel acumularía un recurso por
            // proceso (design D8). Cada ejecución ya se distingue por su traza y su RunId.
            .ConfigureResource(resource => resource.AddService(ServiceName, autoGenerateServiceInstanceId: false))
            .WithTracing(tracing => tracing.AddSource(AgentTelemetry.SourceName))
            .WithMetrics(metrics => metrics.AddMeter(AgentTelemetry.SourceName))
            .WithLogging(
                configureBuilder: null,
                configureOptions: logging =>
                {
                    // Los scopes llevan RunId y CustomerId; sin el mensaje formateado, el cuerpo del registro sería la
                    // plantilla (A7 y A10 del explore).
                    logging.IncludeScopes = true;
                    logging.IncludeFormattedMessage = true;
                })
            // Sin argumentos: el exportador toma de la configuración el endpoint y, si se definen, el protocolo y las
            // cabeceras estándar de OpenTelemetry.
            .UseOtlpExporter();
    }
}
