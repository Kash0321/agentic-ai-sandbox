namespace Sandbox.Agents.Chat;

/// <summary>
/// Fallo del proveedor del modelo con su tipo y su código HTTP. No guarda el mensaje ni la excepción original
/// del proveedor: pueden contener fragmentos de la API key.
/// </summary>
public sealed class ChatProviderException : Exception
{
    public ChatProviderException(ChatProviderErrorCategory category, int? statusCode)
        : base(Describe(category, statusCode))
    {
        Category = category;
        StatusCode = statusCode;
    }

    public ChatProviderErrorCategory Category { get; }

    public int? StatusCode { get; }

    private static string Describe(ChatProviderErrorCategory category, int? statusCode)
    {
        var status = statusCode is { } code ? $" (HTTP {code})" : string.Empty;

        return category switch
        {
            ChatProviderErrorCategory.Authentication => $"El proveedor del modelo rechazó la autenticación{status}.",
            ChatProviderErrorCategory.RateLimited => $"El proveedor del modelo limitó las peticiones{status}.",
            ChatProviderErrorCategory.Network => "No se pudo conectar con el proveedor del modelo.",
            _ => $"El proveedor del modelo devolvió un error{status}.",
        };
    }
}
