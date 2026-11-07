using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Sandbox.Agents.Tests.Scripting;

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
    public async Task GetResponseAsync_AfterTheLastStep_FailsAsAnExhaustedScript()
    {
        using var client = new ScriptedChatClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetResponseAsync([], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("agotado", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResponseAsync_InTwoClients_NumbersTheCallsPerInstance()
    {
        using var first = new ScriptedChatClient(ScriptStep.ToolCalls(("list_products", new { }), ("get_customer", new { })));
        using var second = new ScriptedChatClient(ScriptStep.ToolCalls(("list_products", new { })));
        var token = TestContext.Current.CancellationToken;

        var firstIds = CallIds(await first.GetResponseAsync([], cancellationToken: token));
        var secondIds = CallIds(await second.GetResponseAsync([], cancellationToken: token));

        Assert.Equal(["call_1", "call_2"], firstIds);
        Assert.Equal(["call_1"], secondIds);
    }

    [Fact]
    public async Task GetResponseAsync_WithTheSameStepTwice_GivesEachCallItsOwnId()
    {
        var step = ScriptStep.ToolCalls(("list_products", new { }));
        using var client = new ScriptedChatClient(step, step);
        var token = TestContext.Current.CancellationToken;

        var firstIds = CallIds(await client.GetResponseAsync([], cancellationToken: token));
        var secondIds = CallIds(await client.GetResponseAsync([], cancellationToken: token));

        Assert.Equal(["call_1"], firstIds);
        Assert.Equal(["call_2"], secondIds);
    }

    [Fact]
    public void GetService_ForTheMetadata_IdentifiesTheScriptedProviderAndModel()
    {
        using var client = new ScriptedChatClient();

        var metadata = client.GetService<ChatClientMetadata>();

        Assert.Equal("scripted", metadata?.ProviderName);
        Assert.Equal("scripted-model", metadata?.DefaultModelId);
    }

    [Fact]
    public void GetService_WithAKey_ReturnsNothing()
    {
        using var client = new ScriptedChatClient();

        Assert.Null(client.GetService(typeof(ChatClientMetadata), serviceKey: "otro"));
    }

    private static string[] CallIds(ChatResponse response) =>
        [.. response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>().Select(call => call.CallId)];
}
