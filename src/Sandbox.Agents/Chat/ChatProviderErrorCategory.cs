namespace Sandbox.Agents.Chat;

/// <summary>Tipo de fallo al llamar al proveedor del modelo, independiente del SDK que se use.</summary>
public enum ChatProviderErrorCategory
{
    Authentication,
    RateLimited,
    ProviderError,
    Network,
}
