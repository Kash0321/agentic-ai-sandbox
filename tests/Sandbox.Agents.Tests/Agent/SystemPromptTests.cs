using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Sandbox.Agents.Agent;
using Sandbox.Agents.Tests.Fakes;
using Sandbox.Agents.Tools;

namespace Sandbox.Agents.Tests.Agent;

public sealed partial class SystemPromptTests
{
    [Fact]
    public async Task RunAsync_Always_SendsTheSystemInstructionsAsTheFirstMessage()
    {
        using var harness = new AgentHarness(new ScriptedChatClient(ScriptStep.Reply("hola")));

        await harness.RunAsync();

        var first = harness.Model.Requests[0][0];
        Assert.Equal(ChatRole.System, first.Role);
        Assert.Equal(SystemPrompt.Text, first.Text);
    }

    [Fact]
    public void Text_Always_MentionsTheIdempotencyKeyAndSpanish()
    {
        Assert.Contains("clave de idempotencia", SystemPrompt.Text, StringComparison.Ordinal);
        Assert.Contains("misma clave", SystemPrompt.Text, StringComparison.Ordinal);
        Assert.Contains("en español", SystemPrompt.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1000")]
    [InlineData("1.000")]
    [InlineData("100")]
    [InlineData("Placed")]
    public void Text_Always_LeavesBusinessLimitsToTheDomain(string businessDetail)
    {
        Assert.DoesNotContain(businessDetail, SystemPrompt.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_WhenItCitesATool_UsesAnExistingName()
    {
        var cited = SnakeCaseWord().Matches(SystemPrompt.Text).Select(match => match.Value).Distinct().ToList();

        Assert.NotEmpty(cited);
        Assert.All(cited, name => Assert.Contains(name, SalesToolNames.All));
    }

    [GeneratedRegex(@"\b[a-z]+(?:_[a-z]+)+\b")]
    private static partial Regex SnakeCaseWord();
}
