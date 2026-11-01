using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sandbox.Agents.Chat;
using Sandbox.Runner;

namespace Sandbox.Scenarios.Tests.Runner;

[Collection(nameof(RunnerHostSerialGroup))]
public sealed class RunnerConfigurationTests
{
    private const string ApiKeyVariable = "DeepSeek__ApiKey";

    [Fact]
    public void CreateBuilder_WithSecretFile_ReadsApiKeyFromIt()
    {
        using var secrets = new TemporarySecretsDirectory();
        secrets.Write(ApiKeyVariable, "sk-from-file");

        var options = ResolveOptions(RunnerHost.CreateBuilder([], secrets.Path));

        Assert.Equal("sk-from-file", options.ApiKey);
    }

    [Fact]
    public void CreateBuilder_WithSecretFileAndEnvironmentVariable_PrefersTheFile()
    {
        using var secrets = new TemporarySecretsDirectory();
        secrets.Write(ApiKeyVariable, "sk-from-file");
        Environment.SetEnvironmentVariable(ApiKeyVariable, "sk-from-environment");

        try
        {
            var options = ResolveOptions(RunnerHost.CreateBuilder([], secrets.Path));

            Assert.Equal("sk-from-file", options.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ApiKeyVariable, null);
        }
    }

    [Fact]
    public void CreateBuilder_WithoutSecretsDirectory_UsesKeyFromAnotherSource()
    {
        var options = ResolveOptions(
            RunnerHost.CreateBuilder(["--DeepSeek:ApiKey=sk-from-command-line"], TemporarySecretsDirectory.Missing()));

        Assert.Equal("sk-from-command-line", options.ApiKey);
    }

    [Theory]
    [InlineData("sk-from-file\n")]
    [InlineData("sk-from-file\r\n")]
    [InlineData("  sk-from-file  \n")]
    public void CreateBuilder_WithWhitespaceAroundKeyInSecretFile_UsesTheTrimmedKey(string content)
    {
        using var secrets = new TemporarySecretsDirectory();
        secrets.Write(ApiKeyVariable, content);

        var options = ResolveOptions(RunnerHost.CreateBuilder([], secrets.Path));

        Assert.Equal("sk-from-file", options.ApiKey);
    }

    [Fact]
    public void CreateBuilder_WithoutSettings_UsesFlashModelWithoutThinking()
    {
        var options = ResolveOptions(
            RunnerHost.CreateBuilder(["--DeepSeek:ApiKey=sk-test"], TemporarySecretsDirectory.Missing()));

        Assert.Equal(new Uri("https://api.deepseek.com"), options.Endpoint);
        Assert.Equal("deepseek-flash", options.Model);
        Assert.False(options.Thinking);
    }

    [Fact]
    public async Task CreateBuilder_WithValidOptions_RegistersTheDeepSeekChatClient()
    {
        using var host = RunnerHost.CreateBuilder(["--DeepSeek:ApiKey=sk-test"], TemporarySecretsDirectory.Missing()).Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        var client = host.Services.GetRequiredService<IChatClient>();
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal("deepseek-flash", client.GetService<ChatClientMetadata>()?.DefaultModelId);
    }

    private static DeepSeekOptions ResolveOptions(HostApplicationBuilder builder)
    {
        using var host = builder.Build();
        return host.Services.GetRequiredService<IOptions<DeepSeekOptions>>().Value;
    }
}
