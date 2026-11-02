using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Tools;

/// <summary>
/// Serialización de los resultados de las tools (design D5): la de <c>Microsoft.Extensions.AI</c> (camelCase, enumerados
/// como texto, sin nulos), compacta y con un codificador que no escapa los acentos. Las opciones por defecto indentan, y
/// cada espacio son tokens que se repiten en cada turno. "Unsafe" se refiere a incrustar el JSON en HTML, y estos
/// resultados solo los lee el modelo.
/// </summary>
public static class ToolJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };
        options.MakeReadOnly();
        return options;
    }
}
