namespace Sandbox.Agents.Chat;

/// <summary>Opciones de conexión con DeepSeek, leídas de la sección <c>DeepSeek</c> de la configuración.</summary>
public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public Uri? Endpoint { get; set; } = new("https://api.deepseek.com");

    public string Model { get; set; } = "deepseek-flash";

    /// <summary>Activa el razonamiento del modelo (<c>thinking</c>). DeepSeek lo activa si no se desactiva expresamente.</summary>
    public bool Thinking { get; set; }

    public string? ApiKey { get; set; }
}
