using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Guardrails;
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

    public AgentHarness(
        ScriptedChatClient model,
        bool thinking = false,
        ISalesQueries? queries = null,
        GuardrailOptions? guardrails = null,
        string customerId = SessionCustomerId)
    {
        Model = model;
        Tools = new ToolHarness(customerId, queries);
        Agent = new SalesAgent(
            model,
            Tools.Tools,
            new SalesSession(customerId),
            Options.Create(guardrails ?? new GuardrailOptions()),
            Options.Create(new DeepSeekOptions { ApiKey = "sk-test", Thinking = thinking }),
            AuditLog.Create(Tools.Logs),
            Tools.Logs.CreateLogger<SalesAgent>());
    }

    public ScriptedChatClient Model { get; }

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
        Model.Dispose();
        Tools.Dispose();
    }
}
