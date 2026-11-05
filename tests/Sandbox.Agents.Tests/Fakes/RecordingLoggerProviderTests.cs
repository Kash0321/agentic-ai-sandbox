using Microsoft.Extensions.Logging;

namespace Sandbox.Agents.Tests.Fakes;

public sealed partial class RecordingLoggerProviderTests
{
    [Fact]
    public void Log_InsideNestedScopes_RecordsPropertiesOfBothScopes()
    {
        using var logs = new RecordingLoggerProvider();
        var outer = logs.CreateLogger("Outer");
        var inner = logs.CreateLogger("Inner");

        using (outer.BeginScope(new Dictionary<string, object?> { ["RunId"] = "r1" }))
        using (outer.BeginScope(new Dictionary<string, object?> { ["CustomerId"] = "CUST-001" }))
        {
            Write(inner, "dentro");
        }

        var entry = Assert.Single(logs.Entries);
        Assert.Equal("r1", entry.Scopes["RunId"]);
        Assert.Equal("CUST-001", entry.Scopes["CustomerId"]);
    }

    [Fact]
    public void Log_AfterScopesEnd_RecordsNoScopes()
    {
        using var logs = new RecordingLoggerProvider();
        var logger = logs.CreateLogger("Any");

        using (logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = "r1" }))
        {
        }

        Write(logger, "fuera");

        Assert.Empty(Assert.Single(logs.Entries).Scopes);
    }

    [Fact]
    public async Task Log_InAwaitedCode_KeepsTheScopeOfTheCaller()
    {
        using var logs = new RecordingLoggerProvider();
        var logger = logs.CreateLogger("Any");

        using (logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = "r1" }))
        {
            await Task.Yield();
            Write(logger, "tras await");
        }

        Assert.Equal("r1", Assert.Single(logs.Entries).Scopes["RunId"]);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Text}")]
    private static partial void Write(ILogger logger, string text);
}
