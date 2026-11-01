using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Guarda en memoria todo lo que se registra. Hace de <see cref="ILoggerFactory"/> en los tests unitarios y de
/// <see cref="ILoggerProvider"/> en los tests del host, sin paquetes adicionales. Se enlaza también en
/// <c>Sandbox.Scenarios.Tests</c>.
/// </summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider, ILoggerFactory
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries);

    public void AddProvider(ILoggerProvider provider) =>
        throw new NotSupportedException("Este registro de pruebas no admite otros proveedores.");

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
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

            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception), properties, exception));
        }
    }
}
