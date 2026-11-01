using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Sandbox.Agents.Chat;

// ChatCompletionOptions.Patch (JsonPatch de System.ClientModel) es experimental (SCME0001). Es la única forma de
// añadir a la petición el campo "thinking", que no existe en la API de OpenAI. Se suprime solo en este fichero
// para que, si la API cambia, el fallo quede aquí y en su test.
#pragma warning disable SCME0001

/// <summary>Desactiva el razonamiento de DeepSeek añadiendo <c>"thinking": {"type": "disabled"}</c> a la petición.</summary>
internal static class ThinkingSwitch
{
    private static readonly BinaryData Disabled = BinaryData.FromString("""{"type":"disabled"}""");

    public static void Disable(ChatOptions options)
    {
        var previousFactory = options.RawRepresentationFactory;

        options.RawRepresentationFactory = client =>
        {
            var completionOptions = previousFactory?.Invoke(client) as ChatCompletionOptions ?? new ChatCompletionOptions();
            completionOptions.Patch.Set("$.thinking"u8, Disabled);
            return completionOptions;
        };
    }
}

#pragma warning restore SCME0001
