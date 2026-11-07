using System.Diagnostics;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Telemetry;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;
using Sandbox.Application.Sales;
using Sandbox.Domain.Sales;

namespace Sandbox.Agents.Tests.Agent;

/// <summary>Trazas del agente (spec <c>agent-observability</c>, design D3 a D5 de <c>add-agent-observability</c>).</summary>
[Collection(nameof(TelemetrySerialGroup))]
public sealed class AgentTelemetryTests
{
    private const string Request = "quiero dos tazas rojas";
    private static readonly object OrderOne = new { orderId = "ORD-0001" };
    private static readonly object CancelOrderOne = new { orderId = "ORD-0001", idempotencyKey = "cancel-1" };
    private static readonly object TwoMugs = new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } }, idempotencyKey = "clave-k1" };

    [Fact]
    public async Task RunAsync_CompletedRun_RecordsTheRootSpanWithRunIdStopReasonCallsAndTokens()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })), ScriptStep.Reply("Catálogo")),
            tracing: AgentTracing.WithoutContent);

        await harness.RunAsync();

        var root = Assert.Single(recorder.Named(AgentTelemetry.InvokeAgentSpanName));
        Assert.Equal(default, root.ParentSpanId);
        Assert.Equal(harness.AuditEvents[0].Scopes["RunId"], root.GetTagItem(AgentTelemetry.RunIdTag));
        Assert.Equal("invoke_agent", root.GetTagItem(AgentTelemetry.OperationNameTag));
        Assert.Equal("sales-agent", root.GetTagItem(AgentTelemetry.AgentNameTag));
        Assert.Equal("Completed", root.GetTagItem(AgentTelemetry.StopReasonTag));
        Assert.Equal(2, root.GetTagItem(AgentTelemetry.ModelCallsTag));
        Assert.Equal(200L, root.GetTagItem(AgentTelemetry.InputTokensTag));
        Assert.Equal(20L, root.GetTagItem(AgentTelemetry.OutputTokensTag));
        Assert.Equal(ActivityStatusCode.Unset, root.Status);
    }

    [Fact]
    public async Task RunAsync_StoppedByAGuardrail_MarksTheRootSpanAsErrorWithTheReason()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })),
                ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { }))),
            guardrails: new GuardrailOptions { MaxModelCalls = 1 },
            tracing: AgentTracing.WithoutContent);

        await harness.RunAsync();

        var root = Assert.Single(recorder.Named(AgentTelemetry.InvokeAgentSpanName));
        Assert.Equal("ModelCallLimit", root.GetTagItem(AgentTelemetry.StopReasonTag));
        Assert.Equal(ActivityStatusCode.Error, root.Status);
    }

    [Fact]
    public async Task RunAsync_WhenTheModelThrows_MarksTheRootSpanWithTheTypeAndWithoutTheMessage()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = new AgentHarness(new ScriptedChatClient(), tracing: AgentTracing.WithoutContent);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunAsync());

        var root = Assert.Single(recorder.Named(AgentTelemetry.InvokeAgentSpanName));
        Assert.Equal(ActivityStatusCode.Error, root.Status);
        Assert.Equal(nameof(InvalidOperationException), root.StatusDescription);
        Assert.DoesNotContain("guion", SpanRecorder.TextOf(root), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithAnApprovalPause_KeepsEverySpanInOneTraceUnderTheRoot()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = CancelHarness(approve: true);
        await PlaceOrderOneAsync(harness);

        await harness.RunAsync("cancela mi pedido");

        var root = Assert.Single(recorder.Named(AgentTelemetry.InvokeAgentSpanName));
        Assert.Equal(3, recorder.Named("chat").Count);
        Assert.Equal(2, recorder.Named("execute_tool").Count);
        Assert.Single(recorder.Named("approval"));
        Assert.Empty(recorder.Named("orchestrate_tools"));
        var children = recorder.Spans.Where(span => span != root).ToList();
        Assert.All(children, span => Assert.Equal(root.SpanId, span.ParentSpanId));
        Assert.All(children, span => Assert.Equal(root.TraceId, span.TraceId));
    }

    [Fact]
    public async Task RunAsync_ApprovedConfirmation_RecordsAnApprovalSpanWithTheDecisionAndWithoutArguments()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = CancelHarness(approve: true);
        await PlaceOrderOneAsync(harness);

        await harness.RunAsync("cancela mi pedido");

        var approval = Assert.Single(recorder.Named("approval"));
        Assert.Equal("approval cancel_order", approval.DisplayName);
        Assert.Equal("cancel_order", approval.GetTagItem(AgentTelemetry.ToolNameTag));
        Assert.Equal("Approved", approval.GetTagItem(AgentTelemetry.ApprovalDecisionTag));
        Assert.DoesNotContain("ORD-0001", SpanRecorder.TextOf(approval), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_DeniedConfirmation_RecordsTheDenialAndNoToolSpanForCancelOrder()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = CancelHarness(approve: false);
        await PlaceOrderOneAsync(harness);

        await harness.RunAsync("cancela mi pedido");

        var approval = Assert.Single(recorder.Named("approval"));
        Assert.Equal("Denied", approval.GetTagItem(AgentTelemetry.ApprovalDecisionTag));
        Assert.Empty(recorder.Named("execute_tool cancel_order"));
        Assert.Equal(OrderStatus.Placed, await StatusOfOrderOneAsync(harness));
    }

    [Fact]
    public async Task RunAsync_ToolWithBusinessError_RecordsItsCodeInTheToolSpan()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.GetOrder, new { orderId = "ORD-9999" })), ScriptStep.Reply("No existe")),
            tracing: AgentTracing.WithoutContent);

        await harness.RunAsync();

        var span = Assert.Single(recorder.Named("execute_tool get_order"));
        Assert.Equal("ORDER_NOT_FOUND", span.GetTagItem(AgentTelemetry.ToolOutcomeTag));
    }

    [Fact]
    public async Task RunAsync_ToolBlockedByTheAllowList_RecordsBlockedByPolicyInTheToolSpan()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)), ScriptStep.Reply("No puedo")),
            guardrails: new GuardrailOptions { AllowedTools = [.. SalesToolNames.ReadOnly] },
            tracing: AgentTracing.WithoutContent);

        await harness.RunAsync();

        var span = Assert.Single(recorder.Named("execute_tool place_order"));
        Assert.Equal("BLOCKED_BY_POLICY", span.GetTagItem(AgentTelemetry.ToolOutcomeTag));
    }

    [Fact]
    public async Task RunAsync_TwoModelCalls_RecordsAChatSpanPerCallWithItsTokensAndTheToolSuccess()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { })).WithTokens(120, 12),
                ScriptStep.Reply("Catálogo", inputTokens: 150, outputTokens: 15)),
            tracing: AgentTracing.WithoutContent);

        await harness.RunAsync();

        var chats = recorder.Named("chat");
        Assert.Equal(2, chats.Count);
        Assert.Equal([120, 150], chats.Select(span => Convert.ToInt64(span.GetTagItem(AgentTelemetry.InputTokensTag), System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal([12, 15], chats.Select(span => Convert.ToInt64(span.GetTagItem(AgentTelemetry.OutputTokensTag), System.Globalization.CultureInfo.InvariantCulture)));
        var tool = Assert.Single(recorder.Named("execute_tool list_products"));
        Assert.Equal(ToolInvocation.Succeeded, tool.GetTagItem(AgentTelemetry.ToolOutcomeTag));
    }

    [Fact]
    public async Task RunAsync_WithoutContentCapture_KeepsRequestPromptArgumentsAndResultsOutOfEverySpan()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)), ScriptStep.Reply("Pedido creado")),
            tracing: AgentTracing.WithoutContent);

        await harness.RunAsync(Request);

        var text = string.Join("\n", recorder.Spans.Select(SpanRecorder.TextOf));
        Assert.NotEmpty(recorder.Named("execute_tool place_order"));
        Assert.DoesNotContain(Request, text, StringComparison.Ordinal);
        Assert.DoesNotContain(SystemPrompt.Text[..40], text, StringComparison.Ordinal);
        Assert.DoesNotContain("SKU-MUG-RED", text, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-k1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ORD-0001", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WithContentCapture_RecordsRequestArgumentsAndResultInTheChatSpans()
    {
        using var recorder = new SpanRecorder(AgentTelemetry.SourceName);
        using var harness = new AgentHarness(
            new ScriptedChatClient(ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)), ScriptStep.Reply("Pedido creado")),
            tracing: AgentTracing.WithContent);

        await harness.RunAsync(Request);

        var chats = string.Join("\n", recorder.Named("chat").Select(SpanRecorder.TextOf));
        Assert.Contains(Request, chats, StringComparison.Ordinal);
        Assert.Contains("SKU-MUG-RED", chats, StringComparison.Ordinal);
        Assert.Contains("ORD-0001", chats, StringComparison.Ordinal);
    }

    private static AgentHarness CancelHarness(bool approve) =>
        new(
            new ScriptedChatClient(
                ScriptStep.ToolCalls((SalesToolNames.GetOrder, OrderOne)),
                ScriptStep.ToolCalls((SalesToolNames.CancelOrder, CancelOrderOne)),
                ScriptStep.Reply("Hecho")),
            approver: new ScriptedToolApprover(approve),
            tracing: AgentTracing.WithoutContent);

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
}
