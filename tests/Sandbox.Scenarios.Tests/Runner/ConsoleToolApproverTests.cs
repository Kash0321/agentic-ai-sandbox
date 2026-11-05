using System.Text.Json;
using Sandbox.Agents.Approval;
using Sandbox.Runner.Agent;

namespace Sandbox.Scenarios.Tests.Runner;

/// <summary>Aprobador de consola del Runner (spec <c>tool-approval</c>, design D5), sin teclado.</summary>
public sealed class ConsoleToolApproverTests : IDisposable
{
    private readonly StringWriter _error = new();

    [Theory]
    [InlineData("s")]
    [InlineData("S")]
    [InlineData("si")]
    [InlineData("  SI  ")]
    [InlineData("sí")]
    [InlineData("Sí")]
    [InlineData("SÍ")]
    public async Task DecideAsync_WithAYes_ShowsTheExactActionAndApproves(string answer)
    {
        var decision = await DecideAsync(answer + Environment.NewLine, Request("ORD-0001"));

        Assert.True(decision.Approved);
        Assert.Equal(ConsoleToolApprover.ConsoleApprover, decision.Approver);
        var shown = _error.ToString();
        Assert.Contains("cancel_order", shown, StringComparison.Ordinal);
        Assert.Contains("\"orderId\":\"ORD-0001\"", shown, StringComparison.Ordinal);
        Assert.Contains("[s/N]", shown, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("n")]
    [InlineData("vale")]
    [InlineData("sí, claro")]
    public async Task DecideAsync_WithAnythingElse_Denies(string answer)
    {
        var decision = await DecideAsync(answer + Environment.NewLine, Request("ORD-0001"));

        Assert.False(decision.Approved);
        Assert.Equal(ConsoleToolApprover.RejectedReason, decision.Reason);
    }

    [Fact]
    public async Task DecideAsync_AtTheEndOfTheInput_Denies()
    {
        var decision = await DecideAsync(string.Empty, Request("ORD-0001"));

        Assert.False(decision.Approved);
    }

    [Fact]
    public async Task DecideAsync_ArgumentWithAControlCharacter_ShowsItEscaped()
    {
        await DecideAsync("n" + Environment.NewLine, Request("ORD-0001\u001b[2J"));

        var shown = _error.ToString();
        Assert.DoesNotContain('\u001b', shown);
        Assert.Contains("\\u001B", shown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_ArgumentWithAccents_ShowsThemReadable()
    {
        await DecideAsync("n" + Environment.NewLine, Request("Lámpara"));

        Assert.Contains("Lámpara", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideAsync_WithoutAnInteractiveConsole_DeniesWithoutReadingTheInput()
    {
        var approver = new ConsoleToolApprover(new ThrowingReader(), _error, isInteractive: () => false);

        var decision = await approver.DecideAsync(Request("ORD-0001"), TestContext.Current.CancellationToken);

        Assert.False(decision.Approved);
        Assert.Equal(ConsoleToolApprover.NonInteractiveApprover, decision.Approver);
        Assert.Equal("sin consola interactiva", decision.Reason);
        Assert.Empty(_error.ToString());
    }

    public void Dispose() => _error.Dispose();

    private async Task<ToolApprovalDecision> DecideAsync(string typed, ToolApprovalRequest request)
    {
        using var input = new StringReader(typed);
        var approver = new ConsoleToolApprover(input, _error, isInteractive: () => true);
        return await approver.DecideAsync(request, TestContext.Current.CancellationToken);
    }

    // Los argumentos llegan del modelo como JsonElement, igual que en el agente.
    private static ToolApprovalRequest Request(string orderId) =>
        new(
            "cancel_order",
            new Dictionary<string, object?>
            {
                ["orderId"] = JsonSerializer.SerializeToElement(orderId),
                ["idempotencyKey"] = JsonSerializer.SerializeToElement("cancel-1"),
            });

    private sealed class ThrowingReader : TextReader
    {
        public override string ReadLine() => throw new InvalidOperationException("No se debía leer la entrada.");

        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No se debía leer la entrada.");
    }
}
