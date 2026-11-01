using Microsoft.Extensions.Logging;

namespace Sandbox.Agents.Tests.Fakes;

internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception);
