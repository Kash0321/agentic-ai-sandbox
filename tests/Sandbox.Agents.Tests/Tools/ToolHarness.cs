using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.Auditing;
using Sandbox.Application.FaultInjection;
using Sandbox.Application.Sales;

namespace Sandbox.Agents.Tests.Tools;

/// <summary>
/// Las seis tools sobre un catálogo semilla propio del test, con el inyector de fallos y un registro en memoria.
/// <see cref="Queries"/> lee el estado sin inyector, para comprobar los efectos.
/// </summary>
internal sealed class ToolHarness : IDisposable
{
    public ToolHarness(string customerId = "CUST-001", params FaultRule[] rules)
        : this(customerId, queries: null, rules)
    {
    }

    public ToolHarness(string customerId, ISalesQueries? queries, params FaultRule[] rules)
    {
        var state = SeedCatalog.CreateState();
        var faults = new FaultInjectionOptions();
        foreach (var rule in rules)
        {
            faults.Rules.Add(rule);
        }

        var injector = new FaultInjector(Options.Create(faults), TimeProvider.System, NullLogger<FaultInjector>.Instance);
        var audit = AuditLog.Create(Logs);
        Queries = new SalesQueries(state, audit);
        Commands = new SalesCommands(state, TimeProvider.System, audit);
        Tools = new SalesTools(
            new FaultInjectingSalesCommands(Commands, injector),
            queries ?? new FaultInjectingSalesQueries(Queries, injector),
            new SalesSession(customerId),
            Logs.CreateLogger<SalesTools>());
        Functions = Tools.CreateFunctions().ToDictionary(function => function.Name, StringComparer.Ordinal);
    }

    public RecordingLoggerProvider Logs { get; } = new();

    public SalesQueries Queries { get; }

    /// <summary>Commands sobre el mismo estado, sin inyector: para preparar pedidos de otros clientes.</summary>
    public SalesCommands Commands { get; }

    public SalesTools Tools { get; }

    public IReadOnlyDictionary<string, AIFunction> Functions { get; }

    /// <summary>Invoca una tool como lo haría el bucle: argumentos JSON y resultado serializado.</summary>
    public async Task<JsonElement> InvokeAsync(string toolName, object? arguments = null)
    {
        var values = JsonSerializer.SerializeToElement(arguments ?? new { })
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());

        var result = await Functions[toolName].InvokeAsync(new AIFunctionArguments(values), TestContext.Current.CancellationToken);
        return Assert.IsType<JsonElement>(result);
    }

    public static string? ErrorCode(JsonElement result) =>
        result.TryGetProperty("error", out var error) ? error.GetProperty("code").GetString() : null;

    public static bool IsRetryable(JsonElement result) => result.GetProperty("error").GetProperty("retryable").GetBoolean();

    public void Dispose() => Logs.Dispose();
}
