using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Cliente de chat falso, determinista y sin red. Responde un texto programado o lanza una excepción programada,
/// y guarda los mensajes que recibe. Se enlaza también en <c>Sandbox.Scenarios.Tests</c>.
/// </summary>
internal sealed class FakeChatClient : IChatClient
{
    private readonly Func<ChatResponse> _respond;
    private readonly List<ChatMessage> _receivedMessages = [];

    private FakeChatClient(Func<ChatResponse> respond) => _respond = respond;

    public IReadOnlyList<ChatMessage> ReceivedMessages => _receivedMessages;

    public static FakeChatClient RespondingWith(
        string text,
        string modelId = "fake-model",
        long inputTokens = 5,
        long outputTokens = 1) =>
        new(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = modelId,
            Usage = new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens,
                TotalTokenCount = inputTokens + outputTokens,
            },
        });

    public static FakeChatClient Throwing(Exception exception) => new(() => throw exception);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _receivedMessages.AddRange(messages);

        try
        {
            return Task.FromResult(_respond());
        }
        catch (Exception exception)
        {
            return Task.FromException<ChatResponse>(exception);
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("El cliente falso no simula streaming.");

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
