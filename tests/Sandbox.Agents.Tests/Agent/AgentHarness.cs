using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tests.Tools;
using Sandbox.Application.Sales;

namespace Sandbox.Agents.Tests.Agent;

/// <summary>El agente con un modelo falso con guion y las tools reales sobre un catálogo semilla propio.</summary>
internal sealed class AgentHarness : IDisposable
{
    public AgentHarness(ScriptedChatClient model, bool thinking = false, ISalesQueries? queries = null)
    {
        Model = model;
        Tools = new ToolHarness("CUST-001", queries);
        Agent = new SalesAgent(
            model,
            Tools.Tools,
            Options.Create(new DeepSeekOptions { ApiKey = "sk-test", Thinking = thinking }),
            Tools.Logs.CreateLogger<SalesAgent>());
    }

    public ScriptedChatClient Model { get; }

    public ToolHarness Tools { get; }

    public SalesAgent Agent { get; }

    public string LoggedText =>
        string.Join("\n", Tools.Logs.Entries.Select(entry => $"{entry.Message} {string.Join(" ", entry.Properties.Values)}"));

    public Task<AgentRunResult> RunAsync(string request = "Quiero dos tazas rojas") =>
        Agent.RunAsync(request, TestContext.Current.CancellationToken);

    public void Dispose()
    {
        Model.Dispose();
        Tools.Dispose();
    }
}
