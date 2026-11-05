using System.Text.Json;
using Microsoft.Extensions.AI;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.Sales;

namespace Sandbox.Agents.Tests.Agent;

/// <summary>Allow-list de tools y tools desconocidas (spec <c>agent-guardrails</c>, design D8 y D9).</summary>
public sealed class AgentAllowListTests
{
    private static readonly object TwoMugs = new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } }, idempotencyKey = "k1" };

    [Fact]
    public async Task RunAsync_WithDefaultOptions_OffersTheSixTools()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(ScriptStep.Reply("Hola")));

        await harness.RunAsync();

        Assert.Equal(SalesToolNames.All, OfferedTools(harness));
    }

    [Fact]
    public async Task RunAsync_WithAnAllowList_OffersOnlyThoseTools()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.Reply("Hola")),
            guardrails: new GuardrailOptions { AllowedTools = ["list_products", "get_stock"] });

        await harness.RunAsync();

        Assert.Equal(["list_products", "get_stock"], OfferedTools(harness));
    }

    [Fact]
    public async Task RunAsync_ModelAsksForAToolOutsideTheAllowList_ReceivesBlockedByPolicyAndNothingChanges()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)),
                ScriptStep.Reply("No puedo hacer pedidos")),
            guardrails: new GuardrailOptions { AllowedTools = [.. SalesToolNames.ReadOnly] });

        var result = await harness.RunAsync();

        var blocked = Assert.IsType<JsonElement>(Assert.Single(harness.Model.ReceivedToolResults).Result);
        Assert.Equal("BLOCKED_BY_POLICY", blocked.GetProperty("error").GetProperty("code").GetString());
        Assert.False(blocked.GetProperty("error").GetProperty("retryable").GetBoolean());
        Assert.Equal([new ToolInvocation("place_order", "BLOCKED_BY_POLICY")], result.ToolInvocations);
        var token = TestContext.Current.CancellationToken;
        Assert.False((await harness.Tools.Queries.GetOrderAsync(new GetOrderQuery("ORD-0001", "CUST-001"), token)).IsSuccess);
        Assert.Equal(20, (await harness.Tools.Queries.GetStockAsync(new GetStockQuery("SKU-MUG-RED"), token)).Value?.Available);
    }

    [Fact]
    public async Task RunAsync_ModelAsksForAnInventedTool_RecordsItAsUnknownAndCompletes()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls(("ship_order", new { orderId = "ORD-0001" })),
            ScriptStep.Reply("No puedo enviar pedidos")));

        var result = await harness.RunAsync();

        Assert.Equal(2, harness.Model.Requests.Count);
        Assert.Equal([new ToolInvocation("ship_order", "UNKNOWN_TOOL")], result.ToolInvocations);
        Assert.Equal(AgentStopReason.Completed, result.StopReason);
        var unknown = Assert.Single(harness.AuditEvents, entry => entry.EventId.Name == "UnknownToolRequested");
        Assert.Equal("ship_order", unknown.Properties["Tool"]);
    }

    [Fact]
    public async Task RunAsync_UnknownToolWithAVeryLongName_RecordsTheNameTrimmedTo64Characters()
    {
        var longName = new string('x', 200);
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((longName, new { })),
            ScriptStep.Reply("fin")));

        var result = await harness.RunAsync();

        var unknown = Assert.Single(harness.AuditEvents, entry => entry.EventId.Name == "UnknownToolRequested");
        Assert.Equal(64, Assert.IsType<string>(unknown.Properties["Tool"]).Length);
        Assert.Equal(64, Assert.Single(result.ToolInvocations).Name.Length);
    }

    [Fact]
    public async Task RunAsync_UnknownToolBetweenKnownOnes_KeepsTheOrderOfTheRequests()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { }), ("ship_order", new { }), (SalesToolNames.GetCustomer, new { })),
            ScriptStep.Reply("fin")));

        var result = await harness.RunAsync();

        Assert.Equal(["list_products", "ship_order", "get_customer"], result.ToolInvocations.Select(invocation => invocation.Name));
    }

    private static IEnumerable<string> OfferedTools(AgentHarness harness) =>
        (harness.Model.RequestOptions[0]?.Tools ?? []).Select(tool => tool.Name);
}
