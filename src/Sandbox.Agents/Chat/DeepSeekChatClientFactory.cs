using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace Sandbox.Agents.Chat;

/// <summary>Construye el <see cref="IChatClient"/> hacia DeepSeek con el SDK de OpenAI y sus decoradores.</summary>
public static class DeepSeekChatClientFactory
{
    /// <param name="options">Opciones ya validadas con <see cref="DeepSeekOptionsValidator"/>.</param>
    /// <param name="loggerFactory">Para el registro de cada llamada.</param>
    /// <param name="httpClient">Transporte HTTP opcional; si es <see langword="null"/>, el SDK usa el suyo.</param>
    public static IChatClient Create(DeepSeekOptions options, ILoggerFactory loggerFactory, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = options.Endpoint,
            // Sin reintentos implícitos del SDK: cada reintento cuesta tokens y no queda registrado.
            // La política de reintentos es de la fase 5.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };

        if (httpClient is not null)
        {
            clientOptions.Transport = new HttpClientPipelineTransport(httpClient);
        }

        var apiKey = options.ApiKey ?? throw new ArgumentException("Falta la API key.", nameof(options));
        var providerClient = new ChatClient(options.Model, new ApiKeyCredential(apiKey), clientOptions).AsIChatClient();

        var builder = providerClient.AsBuilder()
            .Use(inner => new ProviderErrorTranslatingChatClient(inner))
            .Use(inner => new UsageLoggingChatClient(inner, loggerFactory.CreateLogger<UsageLoggingChatClient>()));

        if (!options.Thinking)
        {
            builder = builder.ConfigureOptions(ThinkingSwitch.Disable);
        }

        return builder.Build();
    }
}
