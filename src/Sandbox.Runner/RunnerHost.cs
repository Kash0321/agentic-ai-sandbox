using System.Globalization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Approval;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Scripting;
using Sandbox.Agents.Telemetry;
using Sandbox.Agents.Tools;
using Sandbox.Application;
using Sandbox.Application.FaultInjection;
using Sandbox.Runner.Agent;
using Sandbox.Runner.Scenarios;
using Sandbox.Runner.Telemetry;

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
        // La configuración ignora los argumentos posicionales (como la suborden list): se añaden aparte para validarlos.
        var positional = AgentOptions.PositionalArguments(args);
        if (positional.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(
                [new($"{AgentOptions.SectionName}:{nameof(AgentOptions.Command)}", string.Join(' ', positional))]);
        }

        builder.Configuration.AddKeyPerFile(secretsDirectory, optional: true);

        // La salida estándar queda solo para el resultado; todos los registros van a la de errores.
        builder.Services.Configure<ConsoleLoggerOptions>(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
        // Cada línea registrada durante una ejecución del agente muestra su identificador de ejecución y su cliente
        // (scope de auditoría, design D4 de add-agent-guardrails). AddSimpleConsole fija además el formateador por
        // nombre: sin él, la consola copia sus opciones heredadas sobre las del formateador y no muestra los scopes.
        builder.Logging.AddSimpleConsole(options => options.IncludeScopes = true);
        // RunAsync informa de los fallos de arranque con su código de salida y registra los inesperados con su
        // detalle; sin este filtro, una clave ausente mostraría además la traza completa del host.
        builder.Logging.AddFilter("Microsoft.Extensions.Hosting.Internal.Host", LogLevel.None);

        builder.Services.AddOptions<DeepSeekOptions>()
            .Bind(builder.Configuration.GetSection(DeepSeekOptions.SectionName))
            // Un fichero de secreto suele acabar en salto de línea, y la clave no debe llevarlo.
            .PostConfigure(options => options.ApiKey = options.ApiKey?.Trim())
            .ValidateOnStart();
        var configuration = builder.Configuration;
        // Se leen el escenario, el modelo y la suborden de la configuración, y no de AgentOptions, para no encadenar su
        // validación con la del inyector, la de los guardrails ni la de DeepSeek.
        string? AgentSetting(string property) => configuration[$"{AgentOptions.SectionName}:{property}"];
        AgentScenario? CurrentScenario() => ScenarioCatalog.Find(AgentSetting(nameof(AgentOptions.Scenario)));
        bool UsesScriptedModel() => AgentOptions.IsScripted(AgentSetting(nameof(AgentOptions.ModelSource)));
        // Con el modelo con guion o con list no se exige la clave ni se construye el cliente del proveedor (design D6).
        bool UsesProvider() => !UsesScriptedModel() && !AgentOptions.IsList(AgentSetting(nameof(AgentOptions.Command)));

        builder.Services.AddSingleton<IValidateOptions<DeepSeekOptions>>(
            new ProviderOptionsValidator(new DeepSeekOptionsValidator(), UsesProvider));
        builder.Services.AddOptions<TelemetryOptions>()
            .Bind(builder.Configuration.GetSection(TelemetryOptions.SectionName));
        builder.Services.AddSingleton(services =>
        {
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var telemetry = services.GetRequiredService<IOptions<TelemetryOptions>>().Value;
            if (UsesScriptedModel())
            {
                var script = CurrentScenario()?.Script
                    ?? throw new InvalidOperationException("El modelo con guion necesita un escenario.");
                return ChatClientPipeline.CreateBuilder(new ScriptedChatClient([.. script]), loggerFactory, telemetry).Build();
            }

            return DeepSeekChatClientFactory.Create(
                services.GetRequiredService<IOptions<DeepSeekOptions>>().Value,
                loggerFactory,
                telemetry: telemetry);
        });
        builder.Services.AddSingleton<GreetingRunner>();

        builder.Services.AddSandboxApplication()
            .Bind(configuration.GetSection(FaultInjectionOptions.SectionName))
            // Los fallos de un escenario se suman a los configurados, sin pisar sus índices.
            .PostConfigure(faults =>
            {
                foreach (var rule in CurrentScenario()?.CreateFaults() ?? [])
                {
                    faults.Rules.Add(rule);
                }
            });

        AddAgent(builder.Services, CurrentScenario);
        builder.AddRunnerTelemetry();

        return builder;
    }

    /// <summary>Cliente de sesión: el indicado; si no, el del escenario; si tampoco, el cliente por defecto.</summary>
    internal static string ResolveCustomerId(AgentOptions options) =>
        options.CustomerId
        ?? ScenarioCatalog.Find(options.Scenario)?.CustomerId
        ?? AgentOptions.DefaultCustomerId;

    private static void AddAgent(IServiceCollection services, Func<AgentScenario?> currentScenario)
    {
        services.AddOptions<AgentOptions>()
            .BindConfiguration(AgentOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AgentOptions>, AgentOptionsValidator>();
        services.AddOptions<GuardrailOptions>()
            .BindConfiguration(GuardrailOptions.SectionName)
            // Las tools de un escenario reducen la allow-list configurada, nunca la amplían (design D5). La validación
            // corre después, así que una intersección vacía también da el código 2.
            .PostConfigure(guardrails =>
            {
                if (currentScenario()?.AllowedTools is { } scenarioTools)
                {
                    guardrails.AllowedTools =
                        [.. guardrails.EffectiveAllowedTools.Where(tool => scenarioTools.Contains(tool, StringComparer.Ordinal))];
                }
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GuardrailOptions>, GuardrailOptionsValidator>();
        services.AddSingleton(provider => new SalesSession(
            ResolveCustomerId(provider.GetRequiredService<IOptions<AgentOptions>>().Value)));
        services.AddSingleton<SalesTools>();
        services.AddSingleton<IToolApprover>(_ => ConsoleToolApprover.ForProcessConsole());
        services.AddSingleton<SalesAgent>();
        services.AddSingleton<AgentRunner>();
    }

    /// <summary>
    /// Arranca el host (lo que valida las opciones), ejecuta la suborden <c>list</c>, el agente o el saludo, y devuelve
    /// el código de salida. Los mensajes de error son breves y nunca incluyen trazas ni secretos; el detalle va al
    /// registro.
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
            ReportTelemetry(host.Services, logger);
            var exitCode = ExitCodes.Success;
            var agentOptions = host.Services.GetRequiredService<IOptions<AgentOptions>>().Value;
            if (agentOptions.ListsScenarios)
            {
                await WriteScenarioListAsync(output);
            }
            else if (agentOptions.RunsAgent)
            {
                var result = await host.Services.GetRequiredService<AgentRunner>().RunAsync(output, cancellationToken);
                exitCode = await ExitCodeForAsync(result, error);
            }
            else
            {
                await host.Services.GetRequiredService<GreetingRunner>().RunAsync(output, cancellationToken);
            }

            await host.StopAsync(cancellationToken);
            return exitCode;
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

    /// <summary>
    /// Dice hacia dónde se exporta la telemetría y si lleva contenido (design D5 y D6 de add-agent-observability): un
    /// endpoint sin receptor alarga varios segundos la salida sin ningún otro aviso.
    /// </summary>
    private static void ReportTelemetry(IServiceProvider services, ILogger logger)
    {
        if (OtlpExportOptions.Parse(services.GetRequiredService<IOptions<OtlpExportOptions>>().Value.Endpoint) is not { } endpoint)
        {
            return;
        }

        // Se registra una sola vez por ejecución: el coste de describirlo no importa.
        var destination = OtlpExportOptions.Describe(endpoint);
        LogTelemetryEnabled(logger, destination);
        if (services.GetRequiredService<IOptions<TelemetryOptions>>().Value.CaptureContent)
        {
            LogTelemetryWithContent(logger);
        }
    }

    /// <summary>Una línea por escenario, sin cabecera y con el identificador primero: la CI toma la primera columna.</summary>
    private static async Task WriteScenarioListAsync(TextWriter output)
    {
        var idWidth = ScenarioCatalog.All.Max(scenario => scenario.Id.Length);
        foreach (var scenario in ScenarioCatalog.All)
        {
            await output.WriteLineAsync($"{scenario.Id.PadRight(idWidth)}  {scenario.CustomerId}  {scenario.Description}");
        }
    }

    /// <summary>
    /// Código de una ejecución del agente (design D5 de <c>add-scenario-engine</c>): 5 si falla alguna comprobación de
    /// seguridad, si no 4 si la paró un guardrail, y si no 0. No cumplir el objetivo no cambia el código.
    /// </summary>
    private static async Task<int> ExitCodeForAsync(AgentRunnerResult result, TextWriter error)
    {
        var stoppedByGuardrail = result.Run.StopReason is not AgentStopReason.Completed;
        if (stoppedByGuardrail)
        {
            await error.WriteLineAsync($"La ejecución se detuvo por un guardrail: {result.Run.StopReason}");
        }

        if (result.Verdict is { IsSafe: false } verdict)
        {
            await error.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"El escenario no es seguro: {verdict.FailedSafetyChecks} comprobaciones de seguridad fallidas"));
            return ExitCodes.UnsafeScenario;
        }

        return stoppedByGuardrail ? ExitCodes.StoppedByGuardrail : ExitCodes.Success;
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Telemetría OTLP activa hacia {Endpoint}")]
    private static partial void LogTelemetryEnabled(ILogger logger, string endpoint);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "La telemetría incluye el contenido de las conversaciones (Telemetry:CaptureContent); no la actives con datos reales")]
    private static partial void LogTelemetryWithContent(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado en el Runner")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
