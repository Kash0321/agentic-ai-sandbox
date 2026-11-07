using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Tests.Fakes;

public sealed class ScriptedChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_WithTwoSteps_ReturnsEachStepAndRecordsTheRequests()
    {
        using var client = new ScriptedChatClient(
            ScriptStep.ToolCallsWithText("Voy a mirar", ("list_products", new { })),
            ScriptStep.Reply("Hecho", inputTokens: 300, outputTokens: 7));
        var token = TestContext.Current.CancellationToken;

        var first = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hola")], cancellationToken: token);
        var second = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "sigue")], cancellationToken: token);

        var call = Assert.Single(first.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        Assert.Equal("list_products", call.Name);
        Assert.Equal("Voy a mirar", first.Text);
        Assert.Equal("Hecho", second.Text);
        Assert.Equal(300, second.Usage?.InputTokenCount);
        Assert.Equal(["hola", "sigue"], client.Requests.Select(request => Assert.Single(request).Text));
    }

    [Fact]
    public async Task GetResponseAsync_WithObjectArguments_DeliversThemAsJsonElements()
    {
        using var client = new ScriptedChatClient(
            ScriptStep.ToolCalls(("get_stock", new { sku = "SKU-MUG-RED" })));

        var response = await client.GetResponseAsync([], cancellationToken: TestContext.Current.CancellationToken);

        var call = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        var sku = Assert.IsType<JsonElement>(call.Arguments?["sku"]);
        Assert.Equal("SKU-MUG-RED", sku.GetString());
    }

    [Fact]
    public async Task GetResponseAsync_AfterTheLastStep_Fails()
    {
        using var client = new ScriptedChatClient();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetResponseAsync([], cancellationToken: TestContext.Current.CancellationToken));
    }
}
