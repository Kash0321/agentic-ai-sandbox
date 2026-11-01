using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Sandbox.Agents.Chat;

/// <summary>
/// Registra el modelo pedido, el que respondió, la duración y los tokens de cada llamada, sin los textos enviados
/// ni recibidos. Avisa si el proveedor atendió la petición con otro modelo (DeepSeek remapea nombres antiguos).
/// </summary>
internal sealed partial class UsageLoggingChatClient(IChatClient innerClient, ILogger<UsageLoggingChatClient> logger)
    : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var requestedModel = options?.ModelId ?? this.GetService<ChatClientMetadata>()?.DefaultModelId;
        var stopwatch = Stopwatch.StartNew();

        var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        LogCompleted(
            requestedModel,
            response.ModelId,
            stopwatch.ElapsedMilliseconds,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);

        if (response.ModelId is not null && !string.Equals(response.ModelId, requestedModel, StringComparison.Ordinal))
        {
            LogModelMismatch(requestedModel, response.ModelId);
        }

        return response;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Llamada al modelo completada: pedido {RequestedModel}, respondió {ResponseModel}, {ElapsedMilliseconds} ms, {InputTokens} tokens de entrada y {OutputTokens} de salida")]
    private partial void LogCompleted(
        string? requestedModel,
        string? responseModel,
        long elapsedMilliseconds,
        long? inputTokens,
        long? outputTokens);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "El proveedor atendió la petición con otro modelo: se pidió {RequestedModel} y respondió {ResponseModel}")]
    private partial void LogModelMismatch(string? requestedModel, string responseModel);
}
