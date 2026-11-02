using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Tools;
using Sandbox.Application;
using Sandbox.Application.FaultInjection;
using Sandbox.Runner.Agent;

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

        // --scenario, --prompt y --customer se traducen a la sección Agent. Va antes de los secretos, que mantienen la
        // máxima prioridad; las claves no se solapan.
        builder.Configuration.AddCommandLine(args, new Dictionary<string, string>(AgentOptions.CommandLineSwitches));
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

        var configuration = builder.Configuration;
        builder.Services.AddSandboxApplication()
            .Bind(configuration.GetSection(FaultInjectionOptions.SectionName))
            // Los fallos de un escenario se suman a los configurados, sin pisar sus índices. Se lee el escenario de la
            // configuración y no de AgentOptions para no encadenar su validación con la del inyector.
            .PostConfigure(faults =>
            {
                var scenario = ScenarioCatalog.Find(configuration[$"{AgentOptions.SectionName}:{nameof(AgentOptions.Scenario)}"]);
                foreach (var rule in scenario?.CreateFaults() ?? [])
                {
                    faults.Rules.Add(rule);
                }
            });

        AddAgent(builder.Services);

        return builder;
    }

    /// <summary>Cliente de sesión: el indicado; si no, el del escenario; si tampoco, el cliente por defecto.</summary>
    internal static string ResolveCustomerId(AgentOptions options) =>
        options.CustomerId
        ?? ScenarioCatalog.Find(options.Scenario)?.CustomerId
        ?? AgentOptions.DefaultCustomerId;

    private static void AddAgent(IServiceCollection services)
    {
        services.AddOptions<AgentOptions>()
            .BindConfiguration(AgentOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();
        services.AddSingleton(provider => new SalesSession(
            ResolveCustomerId(provider.GetRequiredService<IOptions<AgentOptions>>().Value)));
        services.AddSingleton<SalesTools>();
        services.AddSingleton<SalesAgent>();
        services.AddSingleton<AgentRunner>();
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
            if (host.Services.GetRequiredService<IOptions<AgentOptions>>().Value.RunsAgent)
            {
                await host.Services.GetRequiredService<AgentRunner>().RunAsync(output, cancellationToken);
            }
            else
            {
                await host.Services.GetRequiredService<GreetingRunner>().RunAsync(output, cancellationToken);
            }

            await host.StopAsync(cancellationToken);
            return ExitCodes.Success;
        }
        catch (OptionsValidationException exception)
        {
            return await ReportInvalidConfigurationAsync(error, [exception]);
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(inner => inner is OptionsValidationException))
        {
            // Con más de un tipo de opciones inválido, StartAsync las agrupa (observado en el change add-order-domain).
            return await ReportInvalidConfigurationAsync(error, exception.InnerExceptions.Cast<OptionsValidationException>());
        }
        catch (ChatProviderException exception)
        {
            if (exception.StatusCode is { } statusCode)
            {
                LogModelCallFailedWithStatus(logger, exception.Category, statusCode);
            }
            else
            {
                LogModelCallFailed(logger, exception.Category);
            }

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

    private static async Task<int> ReportInvalidConfigurationAsync(
        TextWriter error,
        IEnumerable<OptionsValidationException> exceptions)
    {
        var failures = exceptions.SelectMany(exception => exception.Failures);
        await error.WriteLineAsync($"Configuración inválida: {string.Join(" ", failures)}");
        return ExitCodes.InvalidConfiguration;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo al llamar al modelo: {Category}, HTTP {StatusCode}")]
    private static partial void LogModelCallFailedWithStatus(ILogger logger, ChatProviderErrorCategory category, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo al llamar al modelo: {Category}, sin respuesta HTTP")]
    private static partial void LogModelCallFailed(ILogger logger, ChatProviderErrorCategory category);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado en el Runner")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
