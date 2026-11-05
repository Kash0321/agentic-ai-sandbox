using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Sandbox.Agents.Guardrails;

namespace Sandbox.Agents.Agent;

/// <summary>
/// Cuenta las llamadas al modelo y los tokens de una ejecución, y la corta antes de superar el máximo de llamadas o el
/// presupuesto de tokens (design D6). Como el uso solo se conoce después de cada llamada, el total puede superar el
/// presupuesto en lo que consuma la última. No deriva de <see cref="DelegatingChatClient"/> porque este liberaría el
/// cliente interior, que es compartido por todas las ejecuciones.
/// </summary>
internal sealed class RunBudgetChatClient(IChatClient innerClient, GuardrailOptions limits) : IChatClient
{
    private int _calls;
    private long _inputTokens;
    private long _outputTokens;

    public int Calls => _calls;

    public long InputTokens => Interlocked.Read(ref _inputTokens);

    public long OutputTokens => Interlocked.Read(ref _outputTokens);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        StartCall();
        var response = await innerClient.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        Add(response.Usage);
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StartCall();
        await foreach (var update in innerClient.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            foreach (var usage in update.Contents.OfType<UsageContent>())
            {
                Add(usage.Details);
            }

            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : innerClient.GetService(serviceType, serviceKey);

    public void Dispose()
    {
    }

    private void StartCall()
    {
        if (_calls >= limits.MaxModelCalls)
        {
            throw new GuardrailStopException(AgentStopReason.ModelCallLimit);
        }

        if (InputTokens + OutputTokens >= limits.TokenBudget)
        {
            throw new GuardrailStopException(AgentStopReason.TokenBudget);
        }

        Interlocked.Increment(ref _calls);
    }

    private void Add(UsageDetails? usage)
    {
        Interlocked.Add(ref _inputTokens, usage?.InputTokenCount ?? 0);
        Interlocked.Add(ref _outputTokens, usage?.OutputTokenCount ?? 0);
    }
}
