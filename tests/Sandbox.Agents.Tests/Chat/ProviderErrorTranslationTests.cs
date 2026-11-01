using System.Net;
using Microsoft.Extensions.AI;
using Sandbox.Agents.Chat;
using Sandbox.Agents.Tests.Fakes;

namespace Sandbox.Agents.Tests.Chat;

public sealed class ProviderErrorTranslationTests
{
    private const string TestApiKey = "sk-test-secret-key-pike";

    [Fact]
    public async Task GetResponseAsync_WhenProviderRejectsKey_ThrowsAuthenticationErrorWithoutKeyFragments()
    {
        using var client = CreateClient(StubHttpMessageHandler.Returning(HttpStatusCode.Unauthorized, RecordedResponses.InvalidApiKey));

        var exception = await Assert.ThrowsAsync<ChatProviderException>(
            () => client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(ChatProviderErrorCategory.Authentication, exception.Category);
        Assert.Equal(401, exception.StatusCode);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("pike", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pike", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, ChatProviderErrorCategory.Authentication)]
    [InlineData(HttpStatusCode.TooManyRequests, ChatProviderErrorCategory.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, ChatProviderErrorCategory.ProviderError)]
    public async Task GetResponseAsync_WhenProviderFails_ThrowsCategoryAndStatus(
        HttpStatusCode status,
        ChatProviderErrorCategory expectedCategory)
    {
        using var client = CreateClient(StubHttpMessageHandler.Returning(status, RecordedResponses.RateLimited));

        var exception = await Assert.ThrowsAsync<ChatProviderException>(
            () => client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(expectedCategory, exception.Category);
        Assert.Equal((int)status, exception.StatusCode);
    }

    [Fact]
    public async Task GetResponseAsync_WhenConnectionFails_ThrowsNetworkError()
    {
        using var client = CreateClient(StubHttpMessageHandler.Throwing(new HttpRequestException("Connection refused (api.deepseek.com:443)")));

        var exception = await Assert.ThrowsAsync<ChatProviderException>(
            () => client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(ChatProviderErrorCategory.Network, exception.Category);
        Assert.Null(exception.StatusCode);
    }

    [Fact]
    public async Task GetResponseAsync_WhenCancelled_PropagatesOperationCanceled()
    {
        using var client = CreateClient(StubHttpMessageHandler.NeverResponding());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetResponseAsync("hola", cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task GetStreamingResponseAsync_WhenProviderRejectsKey_ThrowsAuthenticationErrorWithoutKeyFragments()
    {
        using var client = CreateClient(StubHttpMessageHandler.Returning(HttpStatusCode.Unauthorized, RecordedResponses.InvalidApiKey));

        var exception = await Assert.ThrowsAsync<ChatProviderException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Equal(ChatProviderErrorCategory.Authentication, exception.Category);
        Assert.DoesNotContain("pike", exception.ToString(), StringComparison.Ordinal);
    }

    private static IChatClient CreateClient(StubHttpMessageHandler handler) =>
        DeepSeekChatClientFactory.Create(new DeepSeekOptions { ApiKey = TestApiKey }, new RecordingLoggerProvider(), new HttpClient(handler));
}
