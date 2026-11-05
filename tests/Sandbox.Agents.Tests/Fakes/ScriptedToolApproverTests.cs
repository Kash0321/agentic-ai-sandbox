using Sandbox.Agents.Approval;

namespace Sandbox.Agents.Tests.Fakes;

public sealed class ScriptedToolApproverTests
{
    [Fact]
    public async Task DecideAsync_TwoProgrammedDecisions_ReturnsThemInOrderAndRecordsTheRequests()
    {
        var approver = new ScriptedToolApprover(true, false);
        var token = TestContext.Current.CancellationToken;

        var first = await approver.DecideAsync(Request("ORD-0001"), token);
        var second = await approver.DecideAsync(Request("ORD-0002"), token);

        Assert.True(first.Approved);
        Assert.False(second.Approved);
        Assert.Equal(["ORD-0001", "ORD-0002"], approver.Requests.Select(request => request.Arguments["orderId"]));
    }

    [Fact]
    public async Task DecideAsync_WhenTheScriptIsExhausted_Throws()
    {
        var approver = ScriptedToolApprover.None();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => approver.DecideAsync(Request("ORD-0001"), TestContext.Current.CancellationToken));
    }

    private static ToolApprovalRequest Request(string orderId) =>
        new("cancel_order", new Dictionary<string, object?> { ["orderId"] = orderId });
}
