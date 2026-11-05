using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Modelo falso con guion: cada llamada devuelve el siguiente <see cref="ScriptStep"/> y guarda los mensajes recibidos,
/// para comprobar qué resultados de tools llegaron al modelo. Sin red y determinista. Se enlaza también en
/// <c>Sandbox.Scenarios.Tests</c>.
/// </summary>
internal sealed class ScriptedChatClient(params ScriptStep[] steps) : IChatClient
{
    private readonly Queue<ScriptStep> _steps = new(steps);
    private readonly List<IReadOnlyList<ChatMessage>> _requests = [];
    private readonly List<ChatOptions?> _options = [];

    /// <summary>Mensajes recibidos en cada llamada, en orden.</summary>
    public IReadOnlyList<IReadOnlyList<ChatMessage>> Requests => _requests;

    /// <summary>Opciones recibidas en cada llamada, en orden: entre otras cosas, las tools ofrecidas.</summary>
    public IReadOnlyList<ChatOptions?> RequestOptions => _options;

    /// <summary>Resultados de tools que el modelo ha recibido, en orden de llegada.</summary>
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
            ? Task.FromResult(step.ToResponse())
            : Task.FromException<ChatResponse>(new InvalidOperationException("El guion del modelo falso se ha agotado."));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("El modelo falso con guion no simula streaming.");

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
