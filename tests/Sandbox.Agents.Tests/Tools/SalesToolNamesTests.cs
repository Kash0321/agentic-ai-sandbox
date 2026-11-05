using Sandbox.Agents.Tools;

namespace Sandbox.Agents.Tests.Tools;

public sealed class SalesToolNamesTests
{
    [Fact]
    public void ReadOnly_Always_IsExactlyTheFourQueryTools()
    {
        Assert.Equal(
            ["list_products", "get_stock", "get_customer", "get_order"],
            SalesToolNames.ReadOnly);
    }

    [Fact]
    public void ReadOnly_Always_IsASubsetOfTheCatalog()
    {
        Assert.All(SalesToolNames.ReadOnly, name => Assert.Contains(name, SalesToolNames.All));
    }

    [Fact]
    public void BlockedByPolicy_Always_IsNotRetryableAndDoesNotMentionTheConfiguration()
    {
        var error = ToolError.BlockedByPolicy();

        Assert.Equal("BLOCKED_BY_POLICY", error.Code);
        Assert.False(error.Retryable);
        Assert.DoesNotContain("Guardrails", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AllowedTools", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
