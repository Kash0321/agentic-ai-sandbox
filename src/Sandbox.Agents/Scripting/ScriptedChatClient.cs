using System.Globalization;
using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Scripting;

/// <summary>
/// Modelo con guion: cada llamada devuelve el siguiente <see cref="ScriptStep"/>, sin mirar lo recibido, y guarda los
/// mensajes y las opciones de cada llamada. Sin red y determinista. Sustituye solo al cliente del proveedor: se decora
/// como el real y se identifica como el proveedor <see cref="ProviderName"/> (design D1, D7 y D8 de
/// <c>add-scenario-engine</c>). Agotar el guion es un error del catálogo de escenarios.
/// </summary>
public sealed class ScriptedChatClient(params ScriptStep[] steps) : IChatClient
{
    public const string ProviderName = "scripted";

    private static readonly ChatClientMetadata Metadata = new(ProviderName, null, ScriptStep.ModelId);

    private readonly Queue<ScriptStep> _steps = new(steps);
    private readonly List<IReadOnlyList<ChatMessage>> _requests = [];
    private readonly List<ChatOptions?> _options = [];
    private int _callCounter;

    /// <summary>Mensajes recibidos en cada llamada, en orden.</summary>
    public IReadOnlyList<IReadOnlyList<ChatMessage>> Requests => _requests;

    /// <summary>Opciones recibidas en cada llamada, en orden: entre otras cosas, las tools ofrecidas.</summary>
    public IReadOnlyList<ChatOptions?> RequestOptions => _options;

    /// <summary>Resultados de tools que el modelo ha recibido en la última llamada, en orden de llegada.</summary>
    public IReadOnlyList<FunctionResultContent> ReceivedToolResults =>
        [.. (_requests.LastOrDefault() ?? []).SelectMany(message => message.Contents).OfType<FunctionResultContent>()];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Add([.. messages]);
        _options.Add(options);

        return _steps.TryDequeue(out var step)
            ? Task.FromResult(step.ToResponse(NextCallId))
            : Task.FromException<ChatResponse>(new InvalidOperationException("El guion del modelo se ha agotado."));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("El modelo con guion no simula streaming.");

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceKey is not null)
        {
            return null;
        }

        return serviceType == typeof(ChatClientMetadata) ? Metadata
            : serviceType.IsInstanceOfType(this) ? this
            : null;
    }

    public void Dispose()
    {
    }

    private string NextCallId() =>
        string.Create(CultureInfo.InvariantCulture, $"call_{++_callCounter}");
}
