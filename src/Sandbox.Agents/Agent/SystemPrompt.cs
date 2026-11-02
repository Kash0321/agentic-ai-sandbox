namespace Sandbox.Agents.Agent;

/// <summary>
/// Instrucciones del sistema del agente de ventas, en un recurso incrustado para poder leerlas y revisarlas como texto.
/// Orientan al modelo; las reglas de negocio las decide el dominio y no aparecen aquí.
/// </summary>
public static class SystemPrompt
{
    private const string ResourceName = "Sandbox.Agents.SystemPrompt.md";

    public static string Text { get; } = Load();

    private static string Load()
    {
        using var stream = typeof(SystemPrompt).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Falta el recurso incrustado {ResourceName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
