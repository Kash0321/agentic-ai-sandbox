using Microsoft.Extensions.AI;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Learning;

/// <summary>
/// Tests de aprendizaje: fijan cómo trata <c>FunctionInvokingChatClient</c> (Microsoft.Extensions.AI 10.11.0) las tools
/// envueltas en <see cref="ApprovalRequiredAIFunction"/> (experimentos A1 a A4 del design de <c>add-tool-approval</c>).
/// Si una versión nueva lo cambia, fallan aquí y no en el agente.
/// </summary>
public sealed class ToolApprovalLearningTests
{
    private static readonly object OrderOne = new { orderId = "ORD-0001" };

    [Fact]
    public async Task ApprovalRequiredTool_WhenRequested_ReturnsAnApprovalRequestWithoutInvoking()
    {
        var probe = new Probe(ScriptStep.ToolCalls(("cancel_order", OrderOne)), ScriptStep.Reply("fin"));

        var response = await probe.FirstAsync();

        var request = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>());
        var call = Assert.IsType<FunctionCallContent>(request.ToolCall);
        Assert.Equal("cancel_order", call.Name);
        Assert.Empty(probe.Invoked);
        Assert.Single(probe.Model.Requests);
    }

    [Fact]
    public async Task ApprovedRequest_WhenResumed_GoesThroughTheFunctionInvokerAndHidesTheApprovalFromTheModel()
    {
        var probe = new Probe(ScriptStep.ToolCalls(("cancel_order", OrderOne)), ScriptStep.Reply("Cancelado"));

        var response = await probe.ResumeAsync(approve: true);

        Assert.Equal(["cancel_order"], probe.Invoked.Select(invocation => invocation.Name));
        Assert.Equal("Cancelado", response.Text);
        var sentToModel = probe.Model.Requests[^1].SelectMany(message => message.Contents).ToList();
        Assert.Contains(sentToModel, content => content is FunctionResultContent);
        Assert.DoesNotContain(sentToModel, content => content is ToolApprovalRequestContent or ToolApprovalResponseContent);
    }

    [Fact]
    public async Task DeniedRequest_WhenResumed_SkipsTheFunctionInvokerAndSendsTheRejectionWithTheReason()
    {
        var probe = new Probe(ScriptStep.ToolCalls(("cancel_order", OrderOne)), ScriptStep.Reply("No cancelado"));

        await probe.ResumeAsync(approve: false, reason: "motivo de prueba");

        Assert.Empty(probe.Invoked);
        var result = Assert.Single(probe.Model.ReceivedToolResults).Result?.ToString();
        Assert.Contains("rejected", result, StringComparison.Ordinal);
        Assert.Contains("motivo de prueba", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApprovalRequiredTool_WithAnotherCallInTheSameResponse_TurnsBothIntoApprovalRequests()
    {
        var probe = new Probe(ScriptStep.ToolCalls(("ping", new { }), ("cancel_order", OrderOne)), ScriptStep.Reply("fin"));

        var response = await probe.FirstAsync();

        Assert.Equal(
            ["ping", "cancel_order"],
            response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()
                .Select(request => ((FunctionCallContent)request.ToolCall).Name));
    }

    [Fact]
    public async Task ApprovedRequest_WhenResumed_InvokesWithTheArgumentsOfTheApprovalRequest()
    {
        var probe = new Probe(ScriptStep.ToolCalls(("cancel_order", OrderOne)), ScriptStep.Reply("Cancelado"));

        await probe.ResumeAsync(approve: true);

        var invocation = Assert.Single(probe.Invoked);
        Assert.Equal(probe.ApprovedArguments, invocation.Arguments);
        Assert.Equal("ORD-0001", invocation.Arguments["orderId"]?.ToString());
    }

    /// <summary>Bucle con un FunctionInvoker que registra, sobre un modelo con guion.</summary>
    private sealed class Probe(params ScriptStep[] steps)
    {
        private readonly List<ChatMessage> _history = [new(ChatRole.User, "cancela ORD-0001")];
        private readonly ChatOptions _options = new()
        {
            Tools =
            [
                AIFunctionFactory.Create(() => "pong", "ping"),
                new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string orderId) => $"cancelado {orderId}", "cancel_order")),
            ],
        };

        public ScriptedChatClient Model { get; } = new(steps);

        public List<(string Name, Dictionary<string, object?> Arguments)> Invoked { get; } = [];

        public Dictionary<string, object?> ApprovedArguments { get; private set; } = [];

        public Task<ChatResponse> FirstAsync() => Loop().GetResponseAsync(_history, _options, TestContext.Current.CancellationToken);

        public async Task<ChatResponse> ResumeAsync(bool approve, string? reason = null)
        {
            var first = await FirstAsync();
            var requests = first.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();
            ApprovedArguments = new(((FunctionCallContent)requests[0].ToolCall).Arguments ?? new Dictionary<string, object?>(), StringComparer.Ordinal);

            _history.AddRange(first.Messages);
            _history.Add(new ChatMessage(ChatRole.User, [.. requests.Select(request => (AIContent)request.CreateResponse(approve, reason))]));
            return await Loop().GetResponseAsync(_history, _options, TestContext.Current.CancellationToken);
        }

        private FunctionInvokingChatClient Loop() => new(Model)
        {
            FunctionInvoker = (context, cancellationToken) =>
            {
                Invoked.Add((context.Function.Name, new Dictionary<string, object?>(context.Arguments, StringComparer.Ordinal)));
                return context.Function.InvokeAsync(context.Arguments, cancellationToken);
            },
        };
    }
}
