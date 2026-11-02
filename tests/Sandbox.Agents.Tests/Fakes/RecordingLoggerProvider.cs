using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Guarda en memoria todo lo que se registra. Hace de <see cref="ILoggerFactory"/> en los tests unitarios y de
/// <see cref="ILoggerProvider"/> en los tests del host, sin paquetes adicionales. Se enlaza también en
/// <c>Sandbox.Scenarios.Tests</c>. Los <i>scopes</i> se comparten entre todas las categorías y siguen el flujo
/// asíncrono, como en el registro real: una entrada lleva los del agente aunque la registre la aplicación.
/// </summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider, ILoggerFactory, ISupportExternalScope
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly ScopeStack _scopes = new();
    private IExternalScopeProvider? _externalScopes;

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, this);

    public void AddProvider(ILoggerProvider provider) =>
        throw new NotSupportedException("Este registro de pruebas no admite otros proveedores.");

    // Dentro de un host, LoggerFactory gestiona los scopes y los entrega por aquí; fuera, los guarda ScopeStack.
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _externalScopes = scopeProvider;

    public void Dispose()
    {
    }

    private Dictionary<string, object?> CurrentScopes()
    {
        var scopes = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_externalScopes is { } external)
        {
            external.ForEachScope((scope, target) => ScopeStack.Merge(scope, target), scopes);
        }
        else
        {
            _scopes.CopyTo(scopes);
        }

        return scopes;
    }

    private sealed class RecordingLogger(string category, RecordingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => provider._scopes.Push(state);

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

            provider._entries.Enqueue(new LogEntry(
                category,
                logLevel,
                eventId,
                formatter(state, exception),
                properties,
                provider.CurrentScopes(),
                exception));
        }
    }
}
