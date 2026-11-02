using Microsoft.Extensions.Logging;

namespace Sandbox.Application.Tests.Fakes;

public sealed partial class RecordingLoggerTests
{
    [Fact]
    public void Log_InsideNestedScopes_RecordsPropertiesOfBothScopes()
    {
        var logger = new RecordingLogger<RecordingLoggerTests>();

        using (logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = "r1" }))
        using (logger.BeginScope(new Dictionary<string, object?> { ["CustomerId"] = "CUST-001" }))
        {
            Write(logger, "dentro");
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("r1", entry.Scopes["RunId"]);
        Assert.Equal("CUST-001", entry.Scopes["CustomerId"]);
    }

    [Fact]
    public void Log_AfterScopesEnd_RecordsNoScopes()
    {
        var logger = new RecordingLogger<RecordingLoggerTests>();

        using (logger.BeginScope(new Dictionary<string, object?> { ["RunId"] = "r1" }))
        {
        }

        Write(logger, "fuera");

        Assert.Empty(Assert.Single(logger.Entries).Scopes);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Text}")]
    private static partial void Write(ILogger logger, string text);
}
