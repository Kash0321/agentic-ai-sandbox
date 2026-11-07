using Microsoft.Extensions.Options;

namespace Sandbox.Runner.Telemetry;

/// <summary>
/// Valida al arrancar el endpoint OTLP, si tiene valor. El mensaje nombra la opción y no muestra el valor, que podría
/// llevar credenciales embebidas.
/// </summary>
public sealed class OtlpExportOptionsValidator : IValidateOptions<OtlpExportOptions>
{
    public ValidateOptionsResult Validate(string? name, OtlpExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !options.IsEnabled || OtlpExportOptions.Parse(options.Endpoint) is not null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{OtlpExportOptions.EndpointKey} debe ser una URL absoluta http o https.");
    }
}
