using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Sandbox.Agents.Telemetry;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.Sales;
using Sandbox.Runner;
using Sandbox.Runner.Agent;

namespace Sandbox.Scenarios.Tests.Runner;

/// <summary>Telemetría del Runner (spec <c>agent-observability</c>, design D5 a D8 de <c>add-agent-observability</c>).</summary>
[Collection(nameof(RunnerHostSerialGroup))]
public sealed class RunnerTelemetryTests
{
    private const string ValidKeyArgument = "--DeepSeek:ApiKey=sk-test-secret-key-pike";

    // Puerto sin receptor. El tiempo de espera corto evita que el vaciado al terminar alargue el test (unos 9 s por
    // defecto, A9 del explore).
    private static readonly string[] UnreachableEndpoint =
        ["--OTEL_EXPORTER_OTLP_ENDPOINT=http://127.0.0.1:4999", "--OTEL_EXPORTER_OTLP_TIMEOUT=500"];

    private static readonly object TwoMugs = new
    {
        lines = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } },
        idempotencyKey = "tazas-rojas-1",
    };

    [Fact]
    public void CreateBuilder_WithoutTelemetrySection_DoesNotCaptureContent()
    {
        using var host = RunnerHost.CreateBuilder([ValidKeyArgument], TemporarySecretsDirectory.Missing()).Build();

        Assert.False(host.Services.GetRequiredService<IOptions<TelemetryOptions>>().Value.CaptureContent);
    }

    [Fact]
    public void CreateBuilder_WithCaptureContentOnTheCommandLine_CapturesContent()
    {
        using var host = RunnerHost.CreateBuilder(
            [ValidKeyArgument, "--Telemetry:CaptureContent=true"],
            TemporarySecretsDirectory.Missing()).Build();

        Assert.True(host.Services.GetRequiredService<IOptions<TelemetryOptions>>().Value.CaptureContent);
    }

    [Fact]
    public void CreateBuilder_WithoutEndpoint_RegistersNoTelemetryProvider()
    {
        using var host = RunnerHost.CreateBuilder([ValidKeyArgument], TemporarySecretsDirectory.Missing()).Build();

        Assert.Null(host.Services.GetService<TracerProvider>());
        Assert.Null(host.Services.GetService<MeterProvider>());
        Assert.Null(host.Services.GetService<LoggerProvider>());
    }

    [Fact]
    public void CreateBuilder_WithEmptyEndpoint_RegistersNoTelemetryProvider()
    {
        using var host = RunnerHost.CreateBuilder(
            [ValidKeyArgument, "--OTEL_EXPORTER_OTLP_ENDPOINT="],
            TemporarySecretsDirectory.Missing()).Build();

        Assert.Null(host.Services.GetService<TracerProvider>());
    }

    [Fact]
    public void CreateBuilder_WithValidEndpoint_RegistersTracesMetricsAndLogs()
    {
        using var host = RunnerHost.CreateBuilder([ValidKeyArgument, .. UnreachableEndpoint], TemporarySecretsDirectory.Missing()).Build();

        Assert.NotNull(host.Services.GetService<TracerProvider>());
        Assert.NotNull(host.Services.GetService<MeterProvider>());
        Assert.NotNull(host.Services.GetService<LoggerProvider>());
    }

    [Fact]
    public void CreateBuilder_WithValidEndpoint_ExportsLogsWithScopesAndFormattedMessages()
    {
        using var host = RunnerHost.CreateBuilder([ValidKeyArgument, .. UnreachableEndpoint], TemporarySecretsDirectory.Missing()).Build();

        // Sin estas opciones, el panel no vería RunId ni CustomerId y mostraría plantillas en lugar de mensajes (A7 y A10).
        var logging = host.Services.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;
        Assert.True(logging.IncludeScopes);
        Assert.True(logging.IncludeFormattedMessage);
    }

    [Fact]
    public void CreateBuilder_WithValidEndpoint_IdentifiesTheServiceWithoutAnInstanceId()
    {
        using var host = RunnerHost.CreateBuilder([ValidKeyArgument, .. UnreachableEndpoint], TemporarySecretsDirectory.Missing()).Build();

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        var attributes = resource.Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
        Assert.Equal("sandbox-runner", attributes["service.name"]);
        // Un identificador por proceso haría que el panel mostrara un recurso por cada ejecución (design D8).
        Assert.False(attributes.ContainsKey("service.instance.id"));
    }

    [Theory]
    [InlineData("dashboard:18889")]
    [InlineData("ftp://localhost:4317")]
    [InlineData("no es una url")]
    public async Task RunAsync_WithInvalidEndpoint_ReturnsInvalidConfigurationWithoutCallingTheModel(string endpoint)
    {
        using var run = new RunnerRun();
        using var model = new ScriptedChatClient(ScriptStep.Reply("hola"));

        var exitCode = await run.ExecuteAsync(model, ValidKeyArgument, $"--OTEL_EXPORTER_OTLP_ENDPOINT={endpoint}");

        Assert.Equal(ExitCodes.InvalidConfiguration, exitCode);
        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", run.Error, StringComparison.Ordinal);
        Assert.Empty(model.Requests);
    }

    [Fact]
    public async Task RunAsync_WithEmptyEndpoint_RunsWithoutTheTelemetryLine()
    {
        using var run = new RunnerRun();
        using var model = HappyOrderModel();

        var exitCode = await run.ExecuteAsync(model, ValidKeyArgument, "--scenario", "happy-order", "--OTEL_EXPORTER_OTLP_ENDPOINT=");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain(run.Logs.Entries, entry => entry.Message.Contains("Telemetría OTLP", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithAndWithoutExport_PrintsTheSameOutputAndExitCode()
    {
        using var withoutExport = new RunnerRun();
        using var withExport = new RunnerRun();
        using var firstModel = HappyOrderModel();
        using var secondModel = HappyOrderModel();

        var withoutCode = await withoutExport.ExecuteAsync(firstModel, ValidKeyArgument, "--scenario", "happy-order");
        var withCode = await withExport.ExecuteAsync(secondModel, [ValidKeyArgument, "--scenario", "happy-order", .. UnreachableEndpoint]);

        Assert.Equal(ExitCodes.Success, withoutCode);
        Assert.Equal(withoutCode, withCode);
        Assert.Equal(withoutExport.Output, withExport.Output);
    }

    [Fact]
    public async Task RunAsync_WithEndpoint_LogsTheDestinationWithoutCredentials()
    {
        using var run = new RunnerRun();
        using var model = new ScriptedChatClient(ScriptStep.Reply("hola"));

        await run.ExecuteAsync(
            model,
            ValidKeyArgument,
            "--OTEL_EXPORTER_OTLP_ENDPOINT=http://usuario:secreto@127.0.0.1:4999/ruta",
            "--OTEL_EXPORTER_OTLP_TIMEOUT=500");

        var line = Assert.Single(run.Logs.Entries, entry => entry.Message.Contains("Telemetría OTLP activa", StringComparison.Ordinal));
        Assert.Contains("http://127.0.0.1:4999", line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto", line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("usuario", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithoutEndpoint_DoesNotLogTheTelemetryLine()
    {
        using var run = new RunnerRun();
        using var model = new ScriptedChatClient(ScriptStep.Reply("hola"));

        await run.ExecuteAsync(model, ValidKeyArgument);

        Assert.DoesNotContain(run.Logs.Entries, entry => entry.Message.Contains("Telemetría OTLP", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithEndpointAndCaptureContent_WarnsThatTelemetryCarriesContent()
    {
        using var run = new RunnerRun();
        using var model = new ScriptedChatClient(ScriptStep.Reply("hola"));

        await run.ExecuteAsync(model, [ValidKeyArgument, "--Telemetry:CaptureContent=true", .. UnreachableEndpoint]);

        Assert.Single(run.Logs.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("contenido de las conversaciones", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithCaptureContentAndWithoutEndpoint_DoesNotWarn()
    {
        using var run = new RunnerRun();
        using var model = new ScriptedChatClient(ScriptStep.Reply("hola"));

        await run.ExecuteAsync(model, ValidKeyArgument, "--Telemetry:CaptureContent=true");

        Assert.DoesNotContain(run.Logs.Entries, entry => entry.Message.Contains("contenido de las conversaciones", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithCaptureContent_KeepsTheScenarioRequestOutOfTheConsoleLogs()
    {
        using var run = new RunnerRun();
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var model = HappyOrderModel();
        var request = ScenarioCatalog.Find(ScenarioCatalog.HappyOrder)?.Request ?? throw new InvalidOperationException("Falta el escenario.");

        var exitCode = await run.ExecuteAsync(model, ValidKeyArgument, "--scenario", "happy-order", "--Telemetry:CaptureContent=true");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.NotEmpty(run.Logs.Entries);
        Assert.DoesNotContain(run.Logs.Entries, entry =>
            entry.Message.Contains(request, StringComparison.Ordinal)
            || entry.Properties.Values.Any(value => value?.ToString()?.Contains(request, StringComparison.Ordinal) == true));
    }

    private static ScriptedChatClient HappyOrderModel() => new(
        ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
        ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)),
        ScriptStep.Reply("Pedido ORD-0001 creado"));

    /// <summary>Una ejecución del Runner con un modelo falso, su propio estado de ventas y registros en memoria.</summary>
    private sealed class RunnerRun : IDisposable
    {
        private readonly StringWriter _output = new();
        private readonly StringWriter _error = new();

        public RecordingLoggerProvider Logs { get; } = new();

        public string Output => _output.ToString();

        public string Error => _error.ToString();

        public async Task<int> ExecuteAsync(IChatClient model, params string[] args)
        {
            var builder = RunnerHost.CreateBuilder(args, TemporarySecretsDirectory.Missing());
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(Logs);
            builder.Services.AddSingleton(model);
            builder.Services.AddSingleton(SeedCatalog.CreateState());

            var stopwatch = Stopwatch.StartNew();
            var exitCode = await RunnerHost.RunAsync(builder, _output, _error, TestContext.Current.CancellationToken);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"La ejecución tardó {stopwatch.Elapsed}.");
            return exitCode;
        }

        public void Dispose()
        {
            _output.Dispose();
            _error.Dispose();
            Logs.Dispose();
        }
    }
}
