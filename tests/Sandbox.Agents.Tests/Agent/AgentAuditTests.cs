using Microsoft.Extensions.Logging;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.Auditing;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Agents.Tests.Agent;

/// <summary>Auditoría de cada invocación y correlación por ejecución (specs <c>agent-tools</c> y <c>agent-guardrails</c>).</summary>
public sealed class AgentAuditTests
{
    [Fact]
    public async Task RunAsync_FailedInvocation_WritesAToolInvokedEventWithoutArguments()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, new { lines = new[] { new { sku = "SKU-LAMP-DESK", quantity = 1 } }, idempotencyKey = "k-lamp" })),
            ScriptStep.Reply("No hay lámparas")));

        await harness.RunAsync();

        var invoked = Assert.Single(harness.AuditEvents, entry => entry.EventId.Name == "ToolInvoked");
        Assert.Equal(LogLevel.Information, invoked.Level);
        Assert.Equal("place_order", invoked.Properties["Tool"]);
        Assert.Equal(ToolDecision.Allowed, invoked.Properties["Decision"]);
        Assert.Equal(ErrorCodes.InsufficientStock, invoked.Properties["Outcome"]);
        Assert.IsType<long>(invoked.Properties["ElapsedMs"]);
        Assert.DoesNotContain("SKU-LAMP-DESK", Logged(invoked), StringComparison.Ordinal);
        Assert.DoesNotContain("k-lamp", Logged(invoked), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_BlockedInvocation_WritesAToolInvokedEventWithTheBlockedDecision()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } }, idempotencyKey = "k1" })),
                ScriptStep.Reply("No puedo")),
            guardrails: new GuardrailOptions { AllowedTools = [.. SalesToolNames.ReadOnly] });

        await harness.RunAsync();

        var invoked = Assert.Single(harness.AuditEvents, entry => entry.EventId.Name == "ToolInvoked");
        Assert.Equal(ToolDecision.Blocked, invoked.Properties["Decision"]);
        Assert.Equal("BLOCKED_BY_POLICY", invoked.Properties["Outcome"]);
    }

    [Fact]
    public async Task RunAsync_WithAToolAndAnAccessDenied_CorrelatesEveryAuditEventWithTheRunAndTheCustomer()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
                ScriptStep.ToolCalls((SalesToolNames.GetOrder, new { orderId = "ORD-0001" })),
                ScriptStep.Reply("No encuentro ese pedido")),
            customerId: "CUST-002");
        await harness.Tools.Commands.PlaceOrderAsync(
            new PlaceOrderCommand("k0", "CUST-001", [new OrderLineRequest("SKU-MUG-RED", 1)]),
            TestContext.Current.CancellationToken);

        await harness.RunAsync();

        var events = harness.AuditEvents;
        Assert.Equal(["ToolInvoked", "AccessDenied", "ToolInvoked"], events.Select(entry => entry.EventId.Name));
        var runId = Assert.IsType<string>(events[0].Scopes["RunId"]);
        Assert.All(events, entry =>
        {
            Assert.Equal(runId, entry.Scopes["RunId"]);
            Assert.Equal("CUST-002", entry.Scopes["CustomerId"]);
        });
    }

    [Fact]
    public async Task RunAsync_TwoRuns_UseDifferentRunIds()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
            ScriptStep.Reply("uno"),
            ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
            ScriptStep.Reply("dos")));

        await harness.RunAsync();
        await harness.RunAsync();

        var runIds = harness.AuditEvents.Select(entry => entry.Scopes["RunId"]).ToList();
        Assert.Equal(2, runIds.Count);
        Assert.NotEqual(runIds[0], runIds[1]);
    }

    [Fact]
    public async Task RunAsync_WithAnOrder_WritesNoRequestTextNorOrderDataInTheAudit()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 7 } }, idempotencyKey = "clave-secreta" })),
            ScriptStep.Reply("Pedido ORD-0001 creado")));

        await harness.RunAsync("quiero siete tazas rojas");

        Assert.NotEmpty(harness.AuditEvents);
        Assert.All(harness.AuditEvents, entry =>
        {
            var logged = Logged(entry);
            Assert.DoesNotContain("quiero siete tazas rojas", logged, StringComparison.Ordinal);
            Assert.DoesNotContain("SKU-MUG-RED", logged, StringComparison.Ordinal);
            Assert.DoesNotContain("clave-secreta", logged, StringComparison.Ordinal);
            Assert.DoesNotContain("Quantity", entry.Properties.Keys);
        });
    }

    [Fact]
    public async Task RunAsync_OrderOfTheSessionCustomer_WritesNoAccessDenied()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 1 } }, idempotencyKey = "k1" })),
            ScriptStep.ToolCalls((SalesToolNames.GetOrder, new { orderId = "ORD-0001" })),
            ScriptStep.Reply("Tu pedido está en curso")));

        await harness.RunAsync();

        Assert.DoesNotContain(harness.AuditEvents, entry => entry.EventId.Name == "AccessDenied");
        var order = await harness.Tools.Queries.GetOrderAsync(new GetOrderQuery("ORD-0001", "CUST-001"), TestContext.Current.CancellationToken);
        Assert.Equal(OrderStatus.Placed, order.Value?.Status);
    }

    private static string Logged(LogEntry entry) =>
        string.Join(" ", [entry.Message, .. entry.Properties.Values.Select(value => value?.ToString()), .. entry.Scopes.Values.Select(value => value?.ToString())]);
}
