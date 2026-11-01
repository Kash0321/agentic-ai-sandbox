using Microsoft.Extensions.AI;

namespace Sandbox.Runner;

/// <summary>Ejecución de comprobación: un saludo fijo al modelo, cuya respuesta, modelo y tokens se muestran.</summary>
internal sealed class GreetingRunner(IChatClient chatClient)
{
    // Pide una respuesta de una palabra para gastar el mínimo de tokens (unos 15 con deepseek-flash).
    public const string GreetingPrompt = "Responde solo con la palabra: hola";

    public async Task RunAsync(TextWriter output, CancellationToken cancellationToken)
    {
        var response = await chatClient.GetResponseAsync(GreetingPrompt, cancellationToken: cancellationToken);

        await output.WriteLineAsync($"Respuesta: {response.Text}");
        await output.WriteLineAsync($"Modelo: {response.ModelId ?? "(desconocido)"}");
        await output.WriteLineAsync(
            $"Tokens: {response.Usage?.InputTokenCount} de entrada y {response.Usage?.OutputTokenCount} de salida");
    }
}
