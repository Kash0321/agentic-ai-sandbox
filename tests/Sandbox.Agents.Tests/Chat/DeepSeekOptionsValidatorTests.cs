using Sandbox.Agents.Chat;

namespace Sandbox.Agents.Tests.Chat;

public sealed class DeepSeekOptionsValidatorTests
{
    private const string TestApiKey = "sk-test-secret-key-1234";

    private readonly DeepSeekOptionsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithoutApiKey_FailsNamingTheOption(string? apiKey)
    {
        var result = _validator.Validate(null, new DeepSeekOptions { ApiKey = apiKey });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("DeepSeek:ApiKey", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http://api.deepseek.com")]
    [InlineData("/v1")]
    public void Validate_WithNonHttpsOrRelativeEndpoint_FailsNamingTheOption(string endpoint)
    {
        var options = new DeepSeekOptions { ApiKey = TestApiKey, Endpoint = new Uri(endpoint, UriKind.RelativeOrAbsolute) };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("DeepSeek:Endpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithoutEndpoint_Fails()
    {
        var result = _validator.Validate(null, new DeepSeekOptions { ApiKey = TestApiKey, Endpoint = null });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithEmptyModel_FailsNamingTheOption()
    {
        var result = _validator.Validate(null, new DeepSeekOptions { ApiKey = TestApiKey, Model = "" });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("DeepSeek:Model", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithValidOptions_Succeeds()
    {
        var result = _validator.Validate(null, new DeepSeekOptions { ApiKey = TestApiKey });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithInvalidOptions_NoMessageContainsTheApiKey()
    {
        var options = new DeepSeekOptions { ApiKey = TestApiKey, Endpoint = new Uri("http://example.com"), Model = " " };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.DoesNotContain(TestApiKey, result.FailureMessage, StringComparison.Ordinal);
        Assert.All(result.Failures!, failure => Assert.DoesNotContain(TestApiKey, failure, StringComparison.Ordinal));
    }
}
