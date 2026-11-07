namespace Sandbox.Runner.Telemetry;

/// <summary>
/// Destino de la exportación OTLP. Se lee de la opción estándar <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> de OpenTelemetry, que
/// llega por cualquier fuente de configuración: variable de entorno, línea de comandos o secreto (design D6 de
/// <c>add-agent-observability</c>). Vacía o ausente, no se exporta nada.
/// </summary>
public sealed class OtlpExportOptions
{
    /// <summary>Clave de configuración, con el nombre estándar de OpenTelemetry.</summary>
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public string? Endpoint { get; set; }

    /// <summary>Hay exportación cuando el endpoint tiene valor (la validación al arrancar exige que sea válido).</summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>Endpoint válido, o <see langword="null"/> si está vacío o no es una URL absoluta http o https.</summary>
    public static Uri? Parse(string? endpoint) =>
        Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;

    /// <summary>Esquema, host y puerto, sin credenciales embebidas ni ruta, para mostrarlo en el registro.</summary>
    public static string Describe(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return $"{endpoint.Scheme}://{endpoint.Authority}";
    }
}
