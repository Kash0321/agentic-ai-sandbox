using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Agent;

/// <summary>
/// Cuenta las llamadas al modelo de una ejecución; la respuesta del bucle acumula los tokens, pero no las llamadas.
/// No deriva de <see cref="DelegatingChatClient"/> porque este liberaría el cliente interior, que es compartido por
/// todas las ejecuciones.
/// </summary>
internal sealed class CallCountingChatClient(IChatClient innerClient) : IChatClient
{
    private int _calls;

    public int Calls => _calls;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return innerClient.GetResponseAsync(messages, options, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return innerClient.GetStreamingResponseAsync(messages, options, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : innerClient.GetService(serviceType, serviceKey);

    public void Dispose()
    {
    }
}
