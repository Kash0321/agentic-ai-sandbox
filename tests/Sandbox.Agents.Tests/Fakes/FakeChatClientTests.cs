using Microsoft.Extensions.AI;
using Sandbox.Agents.Chat;

namespace Sandbox.Agents.Tests.Fakes;

public sealed class FakeChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_WhenProgrammedWithText_ReturnsItAndRecordsTheMessage()
    {
        using var client = FakeChatClient.RespondingWith("hola");

        var response = await client.GetResponseAsync("saludo", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("hola", response.Text);
        var received = Assert.Single(client.ReceivedMessages);
        Assert.Equal("saludo", received.Text);
    }

    [Fact]
    public async Task GetResponseAsync_WhenProgrammedWithAuthenticationError_ThrowsIt()
    {
        using var client = FakeChatClient.Throwing(new ChatProviderException(ChatProviderErrorCategory.Authentication, 401));

        var exception = await Assert.ThrowsAsync<ChatProviderException>(
            () => client.GetResponseAsync("saludo", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(ChatProviderErrorCategory.Authentication, exception.Category);
        Assert.Equal(401, exception.StatusCode);
    }
}
