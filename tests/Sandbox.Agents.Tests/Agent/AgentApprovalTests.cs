using Microsoft.Extensions.AI;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Approval;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.Auditing;
using Sandbox.Application.Sales;
using Sandbox.Domain.Sales;

namespace Sandbox.Agents.Tests.Agent;

/// <summary>Confirmación humana de las acciones irreversibles (spec <c>tool-approval</c>, design D1 a D6).</summary>
public sealed class AgentApprovalTests
{
    private static readonly object CancelOrderOne = new { orderId = "ORD-0001", idempotencyKey = "cancel-1" };

    [Fact]
    public async Task RunAsync_ToolWithoutConfirmation_DoesNotAskTheApprover()
    {
        var approver = ScriptedToolApprover.None();
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })), ScriptStep.Reply("Catálogo")),
            approver: approver);

        var result = await harness.RunAsync();

        Assert.Empty(approver.Requests);
        Assert.Equal(AgentStopReason.Completed, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_CancelOrderNotAllowed_IsBlockedWithoutAskingTheApprover()
    {
        var approver = ScriptedToolApprover.None();
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)), ScriptStep.Reply("No puedo")),
            guardrails: new GuardrailOptions { AllowedTools = [.. SalesToolNames.ReadOnly] },
            approver: approver);
        await PlaceOrderOneAsync(harness);

        var result = await harness.RunAsync();

        Assert.Empty(approver.Requests);
        Assert.Equal([new ToolInvocation("cancel_order", "BLOCKED_BY_POLICY")], result.ToolInvocations);
        Assert.Equal(OrderStatus.Placed, await StatusOfOrderOneAsync(harness));
    }

    [Fact]
    public async Task RunAsync_Always_OffersCancelOrderWrappedForApprovalWithTheSameSchema()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(ScriptStep.Reply("Hola")));

        await harness.RunAsync();

        var offered = Assert.Single(harness.Model.RequestOptions[0]?.Tools ?? [], tool => tool.Name == "cancel_order");
        var wrapped = Assert.IsType<ApprovalRequiredAIFunction>(offered);
        var original = harness.Tools.Functions["cancel_order"];
        Assert.Equal(original.JsonSchema.GetRawText(), wrapped.JsonSchema.GetRawText());
    }

    [Fact]
    public async Task RunAsync_WhileTheApproverDecides_TheOrderIsStillPlaced()
    {
        AgentHarness? running = null;
        OrderStatus? statusWhileDeciding = null;
        var approver = new CallbackToolApprover(async _ =>
        {
            statusWhileDeciding = await StatusOfOrderOneAsync(running!); // asignado antes de ejecutar
            return ToolApprovalDecision.Approve("Callback");
        });
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)), ScriptStep.Reply("Cancelado")),
            approver: approver);
        running = harness;
        await PlaceOrderOneAsync(harness);

        await harness.RunAsync();

        Assert.Equal(OrderStatus.Placed, statusWhileDeciding);
    }

    [Fact]
    public async Task RunAsync_ApprovedCancellation_CancelsWithTheArgumentsShownToTheApprover()
    {
        var approver = new ScriptedToolApprover(true);
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)), ScriptStep.Reply("Pedido cancelado")),
            approver: approver);
        await PlaceOrderOneAsync(harness);

        var result = await harness.RunAsync();

        var asked = Assert.Single(approver.Requests);
        Assert.Equal("cancel_order", asked.ToolName);
        Assert.Equal("ORD-0001", asked.Arguments["orderId"]?.ToString());
        Assert.Equal(OrderStatus.Cancelled, await StatusOfOrderOneAsync(harness));
        Assert.Equal([new ToolInvocation("cancel_order", "OK")], result.ToolInvocations);
        Assert.Equal("Pedido cancelado", result.FinalAnswer);
        Assert.Equal(AgentStopReason.Completed, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_WithAPause_KeepsIntermediateTextsAndToolOrder()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.GetCustomer, new { })),
                ScriptStep.ToolCallsWithText("Lo cancelo", (SalesToolNames.CancelOrder, CancelOrderOne)),
                ScriptStep.Reply("Hecho")),
            approver: new ScriptedToolApprover(true));
        await PlaceOrderOneAsync(harness);

        var result = await harness.RunAsync();

        Assert.Equal("Hecho", result.FinalAnswer);
        Assert.Equal(["get_customer", "cancel_order"], result.ToolInvocations.Select(invocation => invocation.Name));
        Assert.Single(harness.Tools.Logs.Entries, entry => entry.Message.Contains("Lo cancelo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithAPause_CountsModelCallsAcrossTheResumes()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)),
                ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
                ScriptStep.Reply("no debería llegar")),
            guardrails: new GuardrailOptions { MaxModelCalls = 2 },
            approver: new ScriptedToolApprover(true));
        await PlaceOrderOneAsync(harness);

        var result = await harness.RunAsync();

        Assert.Equal(2, result.ModelCalls);
        Assert.Equal(2, harness.Model.Requests.Count);
        Assert.Equal(AgentStopReason.ModelCallLimit, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_DeniedCancellation_LeavesTheOrderAndTellsTheModel()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)), ScriptStep.Reply("No lo he cancelado")),
            approver: new ScriptedToolApprover(false));
        await PlaceOrderOneAsync(harness);

        var result = await harness.RunAsync();

        Assert.Equal(OrderStatus.Placed, await StatusOfOrderOneAsync(harness));
        var told = Assert.Single(harness.Model.ReceivedToolResults).Result?.ToString();
        Assert.Contains(SalesAgent.DeniedReason, told, StringComparison.Ordinal);
        Assert.Equal([new ToolInvocation("cancel_order", "CONFIRMATION_DENIED")], result.ToolInvocations);
        Assert.Equal(AgentStopReason.Completed, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_QueryAndCancellationTogether_AsksOnlyForTheCancellation()
    {
        var approver = new ScriptedToolApprover(true);
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.GetOrder, new { orderId = "ORD-0001" }), (SalesToolNames.CancelOrder, CancelOrderOne)),
                ScriptStep.Reply("Cancelado")),
            approver: approver);
        await PlaceOrderOneAsync(harness);

        var result = await harness.RunAsync();

        Assert.Equal("cancel_order", Assert.Single(approver.Requests).ToolName);
        Assert.Equal(
            [new ToolInvocation("get_order", "OK"), new ToolInvocation("cancel_order", "OK")],
            result.ToolInvocations);
        Assert.Equal(OrderStatus.Cancelled, await StatusOfOrderOneAsync(harness));
    }

    [Fact]
    public async Task RunAsync_ThreeDenialsInARow_StopWithoutCallingTheModelAgain()
    {
        var approver = new ScriptedToolApprover(false, false, false, false);
        using var harness = new AgentHarness(
            new ScriptedChatClient([.. Enumerable.Range(0, 5).Select(_ => ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)))]),
            guardrails: new GuardrailOptions { MaxConsecutiveToolErrors = 3 },
            approver: approver);
        await PlaceOrderOneAsync(harness);

        var result = await harness.RunAsync();

        Assert.Equal(3, approver.Requests.Count);
        Assert.Equal(3, harness.Model.Requests.Count);
        Assert.Equal(AgentStopReason.ToolErrorLimit, result.StopReason);
        Assert.Equal(OrderStatus.Placed, await StatusOfOrderOneAsync(harness));
    }

    [Fact]
    public async Task RunAsync_DeniedCancellation_WritesAnApprovalDecidedEventWithoutTheArguments()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)), ScriptStep.Reply("No")),
            approver: new ScriptedToolApprover(false));
        await PlaceOrderOneAsync(harness);

        await harness.RunAsync();

        var decided = Assert.Single(harness.AuditEvents, entry => entry.EventId.Name == "ApprovalDecided");
        Assert.Equal("cancel_order", decided.Properties["Tool"]);
        Assert.Equal(ApprovalOutcome.Denied, decided.Properties["Outcome"]);
        Assert.Equal(ScriptedToolApprover.Name, decided.Properties["Approver"]);
        Assert.False(string.IsNullOrEmpty(decided.Scopes["RunId"]?.ToString()));
        var logged = string.Join(" ", decided.Message, string.Join(" ", decided.Properties.Values));
        Assert.DoesNotContain("ORD-0001", logged, StringComparison.Ordinal);
        Assert.DoesNotContain("cancel-1", logged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ApprovedTogetherWithAQuery_AuditsOnlyTheHumanDecision()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.GetOrder, new { orderId = "ORD-0001" }), (SalesToolNames.CancelOrder, CancelOrderOne)),
                ScriptStep.Reply("Cancelado")),
            approver: new ScriptedToolApprover(true));
        await PlaceOrderOneAsync(harness);

        await harness.RunAsync();

        var decided = Assert.Single(harness.AuditEvents, entry => entry.EventId.Name == "ApprovalDecided");
        Assert.Equal(ApprovalOutcome.Approved, decided.Properties["Outcome"]);
        Assert.Equal("sin motivo", decided.Properties["Reason"]);
    }

    private static async Task PlaceOrderOneAsync(AgentHarness harness)
    {
        var placed = await harness.Tools.Commands.PlaceOrderAsync(
            new PlaceOrderCommand("seed-1", AgentHarness.SessionCustomerId, [new OrderLineRequest("SKU-MUG-RED", 2)]),
            TestContext.Current.CancellationToken);
        Assert.Equal("ORD-0001", placed.Value?.OrderId);
    }

    private static async Task<OrderStatus?> StatusOfOrderOneAsync(AgentHarness harness) =>
        (await harness.Tools.Queries.GetOrderAsync(
            new GetOrderQuery("ORD-0001", AgentHarness.SessionCustomerId),
            TestContext.Current.CancellationToken)).Value?.Status;

    private sealed class CallbackToolApprover(Func<ToolApprovalRequest, Task<ToolApprovalDecision>> decide) : IToolApprover
    {
        public Task<ToolApprovalDecision> DecideAsync(ToolApprovalRequest request, CancellationToken cancellationToken) =>
            decide(request);
    }
}
