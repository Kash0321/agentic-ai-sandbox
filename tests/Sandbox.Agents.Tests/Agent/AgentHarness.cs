using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Approval;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Telemetry;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tests.Tools;
using Sandbox.Agents.Tools;
using Sandbox.Application.Auditing;
using Sandbox.Application.Sales;

namespace Sandbox.Agents.Tests.Agent;

/// <summary>El agente con un modelo falso con guion y las tools reales sobre un catálogo semilla propio.</summary>
internal sealed class AgentHarness : IDisposable
{
    public const string SessionCustomerId = "CUST-001";

    private readonly IChatClient? _traced;

    public AgentHarness(
        ScriptedChatClient model,
        bool thinking = false,
        ISalesQueries? queries = null,
        GuardrailOptions? guardrails = null,
        string customerId = SessionCustomerId,
        IToolApprover? approver = null,
        AgentTracing tracing = AgentTracing.None)
    {
        Model = model;
        Approver = approver ?? ScriptedToolApprover.None();
        Tools = new ToolHarness(customerId, queries);
        // Con trazas, el modelo va envuelto como en la factoría: cliente de OpenTelemetry con la fuente del agente.
        _traced = tracing is AgentTracing.None
            ? null
            : model.AsBuilder()
                .UseOpenTelemetry(
                    sourceName: AgentTelemetry.SourceName,
                    configure: client => client.EnableSensitiveData = tracing is AgentTracing.WithContent)
                .Build();
        Agent = new SalesAgent(
            _traced ?? model,
            Tools.Tools,
            new SalesSession(customerId),
            Approver,
            Options.Create(guardrails ?? new GuardrailOptions()),
            Options.Create(new DeepSeekOptions { ApiKey = "sk-test", Thinking = thinking }),
            AuditLog.Create(Tools.Logs),
            Tools.Logs.CreateLogger<SalesAgent>());
    }

    public ScriptedChatClient Model { get; }

    /// <summary>Aprobador de la ejecución; por defecto, uno que falla si se le pregunta.</summary>
    public IToolApprover Approver { get; }

    public ToolHarness Tools { get; }

    public SalesAgent Agent { get; }

    public string LoggedText =>
        string.Join("\n", Tools.Logs.Entries.Select(entry => $"{entry.Message} {string.Join(" ", entry.Properties.Values)}"));

    /// <summary>Eventos de auditoría registrados, de la aplicación y del agente.</summary>
    public IReadOnlyList<LogEntry> AuditEvents =>
        [.. Tools.Logs.Entries.Where(entry => entry.Category == AuditLog.Category)];

    public Task<AgentRunResult> RunAsync(string request = "Quiero dos tazas rojas") =>
        Agent.RunAsync(request, TestContext.Current.CancellationToken);

    public void Dispose()
    {
        _traced?.Dispose();
        Model.Dispose();
        Tools.Dispose();
    }
}
