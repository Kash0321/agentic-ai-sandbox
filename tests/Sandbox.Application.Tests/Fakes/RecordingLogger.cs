using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Sandbox.Application.Tests.Fakes;

/// <summary>
/// Registro de pruebas que guarda en memoria cada entrada con su nivel, su mensaje formateado y sus propiedades
/// estructuradas. Es propio de este proyecto para no acoplarlo a los dobles de <c>Sandbox.Agents.Tests</c> (design D10).
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<RecordedLog> _entries = new();

    public IReadOnlyList<RecordedLog> Entries => [.. _entries];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);

        _entries.Enqueue(new RecordedLog(logLevel, formatter(state, exception), properties));
    }
}
