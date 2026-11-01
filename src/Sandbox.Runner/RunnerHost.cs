using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Chat;

namespace Sandbox.Runner;

/// <summary>Raíz de composición del Runner: fuentes de configuración, servicios y ejecución con códigos de salida.</summary>
public static partial class RunnerHost
{
    /// <summary>Directorio donde Docker y compose montan los secretos de tipo fichero.</summary>
    public const string DefaultSecretsDirectory = "/run/secrets";

    /// <summary>
    /// Crea el builder del host. La configuración se lee, de menor a mayor prioridad, de <c>appsettings.json</c>,
    /// las User Secrets (solo en <c>Development</c>), las variables de entorno, la línea de comandos y los ficheros
    /// de <paramref name="secretsDirectory"/>, que puede no existir.
    /// </summary>
    public static HostApplicationBuilder CreateBuilder(string[] args, string secretsDirectory = DefaultSecretsDirectory)
    {
        // La raíz de contenido es la carpeta del ejecutable, no el directorio actual: así appsettings.json se
        // encuentra igual con "dotnet run --project" desde la raíz del repositorio que dentro del contenedor.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration.AddKeyPerFile(secretsDirectory, optional: true);

        // La salida estándar queda solo para el resultado; todos los registros van a la de errores.
        builder.Services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        // RunAsync informa de los fallos de arranque con su código de salida y registra los inesperados con su
        // detalle; sin este filtro, una clave ausente mostraría además la traza completa del host.
        builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.None);

        builder.Services.AddOptions<DeepSeekOptions>()
            .Bind(builder.Configuration.GetSection(DeepSeekOptions.SectionName))
            // Un fichero de secreto suele acabar en salto de línea, y la clave no debe llevarlo.
            .PostConfigure(options => options.ApiKey = options.ApiKey?.Trim())
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<DeepSeekOptions>, DeepSeekOptionsValidator>();
        builder.Services.AddSingleton(services => DeepSeekChatClientFactory.Create(
            services.GetRequiredService<IOptions<DeepSeekOptions>>().Value,
            services.GetRequiredService<ILoggerFactory>()));
        builder.Services.AddSingleton<GreetingRunner>();

        return builder;
    }

    /// <summary>
    /// Arranca el host (lo que valida las opciones), ejecuta el saludo y devuelve el código de salida.
    /// Los mensajes de error son breves y nunca incluyen trazas ni secretos; el detalle va al registro.
    /// </summary>
    public static async Task<int> RunAsync(
        HostApplicationBuilder builder,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RunnerHost).FullName ?? nameof(RunnerHost));

        try
        {
            await host.StartAsync(cancellationToken);
            await host.Services.GetRequiredService<GreetingRunner>().RunAsync(output, cancellationToken);
            await host.StopAsync(cancellationToken);
            return ExitCodes.Success;
        }
        catch (OptionsValidationException exception)
        {
            await error.WriteLineAsync($"Configuración inválida: {string.Join(" ", exception.Failures)}");
            return ExitCodes.InvalidConfiguration;
        }
        catch (ChatProviderException exception)
        {
            LogModelCallFailed(logger, exception.Category, exception.StatusCode);
            await error.WriteLineAsync($"Fallo al llamar al modelo: {exception.Message}");
            return ExitCodes.ModelCallFailed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUnexpectedError(logger, exception);
            await error.WriteLineAsync("Error inesperado. El detalle está en el registro de errores.");
            return ExitCodes.UnexpectedError;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo al llamar al modelo: {Category}, HTTP {StatusCode}")]
    private static partial void LogModelCallFailed(ILogger logger, ChatProviderErrorCategory category, int? statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado en el Runner")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
