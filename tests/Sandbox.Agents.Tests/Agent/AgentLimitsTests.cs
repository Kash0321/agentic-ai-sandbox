using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tests.Tools;
using Sandbox.Agents.Tools;
using Sandbox.Application.Auditing;
using Sandbox.Domain.Results;

namespace Sandbox.Agents.Tests.Agent;

/// <summary>Límites de llamadas, de errores seguidos, de tokens y de longitud (spec <c>agent-guardrails</c>).</summary>
public sealed class AgentLimitsTests
{
    private static readonly object MissingOrder = new { orderId = "ORD-9999" };

    private static readonly object TwoMugs = new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } }, idempotencyKey = "k1" };

    [Fact]
    public async Task RunAsync_ModelThatKeepsAskingForTools_StopsAtMaxModelCallsWithoutAnotherCall()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient([.. Steps(5, () => ListProducts())]),
            guardrails: new GuardrailOptions { MaxModelCalls = 3 });

        var result = await harness.RunAsync();

        Assert.Equal(3, harness.Model.Requests.Count);
        Assert.Equal(3, result.ModelCalls);
        Assert.Equal(3, result.ToolInvocations.Count(invocation => invocation is { Name: "list_products", Outcome: "OK" }));
        Assert.Equal(AgentStopReason.ModelCallLimit, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_ReplyInTheLastAllowedCall_Completes()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(ListProducts(), ListProducts(), ScriptStep.Reply("Aquí tienes el catálogo")),
            guardrails: new GuardrailOptions { MaxModelCalls = 3 });

        var result = await harness.RunAsync();

        Assert.Equal("Aquí tienes el catálogo", result.FinalAnswer);
        Assert.Equal(AgentStopReason.Completed, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_WithMaxModelCallsTwo_EndsAfterTheSecondCallWithoutAText()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient([.. Steps(4, () => ListProducts())]),
            guardrails: new GuardrailOptions { MaxModelCalls = 2 });

        var result = await harness.RunAsync();

        Assert.Equal(2, harness.Model.Requests.Count);
        Assert.Equal(AgentStopReason.ModelCallLimit, result.StopReason);
        Assert.False(string.IsNullOrWhiteSpace(result.FinalAnswer));
    }

    [Fact]
    public async Task RunAsync_WhenTheTokenBudgetIsReached_DoesNotMakeTheNextCall()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient([.. Steps(4, () => ListProducts().WithTokens(100, 50))]),
            guardrails: new GuardrailOptions { TokenBudget = 300 });

        var result = await harness.RunAsync();

        Assert.Equal(2, harness.Model.Requests.Count);
        Assert.Equal(2, result.ModelCalls);
        Assert.Equal(200, result.InputTokens);
        Assert.Equal(100, result.OutputTokens);
        Assert.Equal(AgentStopReason.TokenBudget, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_HappyPathWithDefaultLimits_Completes()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            ListProducts(),
            ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, new { lines = new[] { new { sku = "SKU-MUG-RED", quantity = 2 } }, idempotencyKey = "k1" })),
            ScriptStep.Reply("Pedido ORD-0001 creado")));

        var result = await harness.RunAsync();

        Assert.Equal(AgentStopReason.Completed, result.StopReason);
        Assert.Equal("Pedido ORD-0001 creado", result.FinalAnswer);
    }

    [Fact]
    public async Task RunAsync_ThreeToolErrorsInARow_StopsWithoutCallingTheModelAgain()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient([.. Steps(5, () => GetMissingOrder())]),
            guardrails: new GuardrailOptions { MaxConsecutiveToolErrors = 3 });

        var result = await harness.RunAsync();

        Assert.Equal(3, harness.Model.Requests.Count);
        Assert.Equal(
            Enumerable.Repeat(new ToolInvocation("get_order", ErrorCodes.OrderNotFound), 3),
            result.ToolInvocations);
        Assert.Equal(AgentStopReason.ToolErrorLimit, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_ThreeBlockedToolsInARow_CountAsErrorsAndStopTheRun()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient([.. Steps(5, () => ScriptStep.ToolCalls((SalesToolNames.PlaceOrder, TwoMugs)))]),
            guardrails: new GuardrailOptions { AllowedTools = [.. SalesToolNames.ReadOnly] });

        var result = await harness.RunAsync();

        Assert.Equal(3, harness.Model.Requests.Count);
        Assert.Equal(
            Enumerable.Repeat(new ToolInvocation("place_order", ToolErrorCodes.BlockedByPolicy), 3),
            result.ToolInvocations);
        Assert.Equal(AgentStopReason.ToolErrorLimit, result.StopReason);
    }

    [Fact]
    public async Task RunAsync_ASuccessBetweenErrors_ResetsTheCount()
    {
        using var harness = new AgentHarness(
            new ScriptedChatClient(
                GetMissingOrder(),
                GetMissingOrder(),
                ListProducts(),
                GetMissingOrder(),
                GetMissingOrder(),
                ScriptStep.Reply("No encuentro ese pedido")),
            guardrails: new GuardrailOptions { MaxConsecutiveToolErrors = 3 });

        var result = await harness.RunAsync();

        Assert.Equal(AgentStopReason.Completed, result.StopReason);
        Assert.Equal("No encuentro ese pedido", result.FinalAnswer);
    }

    [Fact]
    public async Task RunAsync_StoppedByToolErrors_AnswersWithTheFixedTextAndLogsTheModelText()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            [.. Steps(4, () => ScriptStep.ToolCallsWithText("Lo busco", (SalesToolNames.GetOrder, MissingOrder)))]));

        var result = await harness.RunAsync();

        Assert.Equal(AgentStopReason.ToolErrorLimit, result.StopReason);
        Assert.StartsWith("No he podido terminar de atender tu petición", result.FinalAnswer, StringComparison.Ordinal);
        Assert.Contains("se mantienen", result.FinalAnswer, StringComparison.Ordinal);
        Assert.DoesNotContain("Lo busco", result.FinalAnswer, StringComparison.Ordinal);
        Assert.Contains("Lo busco", harness.LoggedText, StringComparison.Ordinal);
        var stopped = Assert.Single(harness.AuditEvents, entry => entry.EventId.Name == "RunStopped");
        Assert.Equal("ToolErrorLimit", stopped.Properties["Reason"]);
    }

    [Fact]
    public async Task RunAsync_StoppedByToolErrors_ReturnsThePartialCallsTokensAndTools()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(
            [.. Steps(4, () => GetMissingOrder().WithTokens(100, 10))]));

        var result = await harness.RunAsync();

        Assert.Equal(3, result.ModelCalls);
        Assert.Equal(300, result.InputTokens);
        Assert.Equal(3, result.ToolInvocations.Count(invocation => invocation.Outcome == ErrorCodes.OrderNotFound));
    }

    [Fact]
    public async Task RunAsync_Completed_WritesNoRunStoppedEvent()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(ScriptStep.Reply("Hola")));

        await harness.RunAsync();

        Assert.DoesNotContain(harness.AuditEvents, entry => entry.EventId.Name == "RunStopped");
    }

    [Fact]
    public async Task RunAsync_RequestLongerThanTheMaximum_IsRejectedWithoutCallingTheModel()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(ScriptStep.Reply("no debería llegar")));

        await Assert.ThrowsAsync<ArgumentException>(() => harness.RunAsync(new string('a', SalesAgent.MaxRequestLength + 1)));

        Assert.Empty(harness.Model.Requests);
    }

    [Fact]
    public async Task RunAsync_RequestOfExactlyTheMaximum_IsAccepted()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(ScriptStep.Reply("Recibido")));

        var result = await harness.RunAsync(new string('a', 2000));

        Assert.Equal("Recibido", result.FinalAnswer);
    }

    [Fact]
    public async Task RunAsync_Always_LeavesTheSharedChatClientUndisposed()
    {
        using var model = new DisposalTrackingChatClient(new ScriptedChatClient(ScriptStep.Reply("Hola")));
        using var tools = new ToolHarness();
        var agent = new SalesAgent(
            model,
            tools.Tools,
            new SalesSession("CUST-001"),
            ScriptedToolApprover.None(),
            Options.Create(new GuardrailOptions()),
            Options.Create(new DeepSeekOptions { ApiKey = "sk-test" }),
            AuditLog.Create(tools.Logs),
            NullLogger<SalesAgent>.Instance);

        await agent.RunAsync("Hola", TestContext.Current.CancellationToken);

        Assert.False(model.Disposed);
    }

    // Cada paso necesita su propia petición: si dos respuestas repiten el mismo CallId, la librería da la segunda por
    // respondida y termina el bucle.
    private static IEnumerable<ScriptStep> Steps(int count, Func<ScriptStep> step) =>
        Enumerable.Range(0, count).Select(_ => step());

    private static ScriptStep ListProducts() => ScriptStep.ToolCalls((SalesToolNames.ListProducts, new { }));

    private static ScriptStep GetMissingOrder() => ScriptStep.ToolCalls((SalesToolNames.GetOrder, MissingOrder));

    private sealed class DisposalTrackingChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
