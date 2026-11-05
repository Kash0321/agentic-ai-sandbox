using Microsoft.Extensions.Logging;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>Una entrada registrada, con las propiedades de los <i>scopes</i> activos al registrarla.</summary>
internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyDictionary<string, object?> Scopes,
    Exception? Exception);
