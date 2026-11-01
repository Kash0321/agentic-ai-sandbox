using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Chat;

/// <summary>
/// Convierte los errores del SDK de OpenAI en <see cref="ChatProviderException"/> sin copiar el texto del proveedor,
/// que puede repetir parte de la API key (DeepSeek devuelve sus 4 últimos caracteres en el 401).
/// </summary>
internal sealed class ProviderErrorTranslatingChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using (updates.ConfigureAwait(false))
        {
            while (true)
            {
                try
                {
                    if (!await updates.MoveNextAsync().ConfigureAwait(false))
                    {
                        yield break;
                    }
                }
                catch (Exception exception) when (Translate(exception) is { } translated)
                {
                    throw translated;
                }

                yield return updates.Current;
            }
        }
    }

    private static ChatProviderException? Translate(Exception exception) => exception switch
    {
        ClientResultException { Status: 0 } => new(ChatProviderErrorCategory.Network, null),
        ClientResultException { Status: 401 or 403 } result => new(ChatProviderErrorCategory.Authentication, result.Status),
        ClientResultException { Status: 429 } result => new(ChatProviderErrorCategory.RateLimited, result.Status),
        ClientResultException result => new(ChatProviderErrorCategory.ProviderError, result.Status),
        HttpRequestException => new(ChatProviderErrorCategory.Network, null),
        _ => null,
    };
}
