using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Sandbox.Application.Tests.Fakes;

/// <summary>
/// Registro de pruebas que guarda en memoria cada entrada con su nivel, su identificador de evento, su mensaje
/// formateado, sus propiedades estructuradas y las de los <i>scopes</i> activos, que siguen el flujo asíncrono. Es
/// propio de este proyecto para no acoplarlo a los dobles de <c>Sandbox.Agents.Tests</c> (design D10 de
/// <c>add-order-domain</c>).
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<RecordedLog> _entries = new();
    private readonly AsyncLocal<Scope?> _scope = new();

    public IReadOnlyList<RecordedLog> Entries => [.. _entries];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        var previous = _scope.Value;
        _scope.Value = new Scope(state, previous);
        return new ScopeEnd(this, previous);
    }

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

        _entries.Enqueue(new RecordedLog(logLevel, eventId, formatter(state, exception), properties, CurrentScopes()));
    }

    private Dictionary<string, object?> CurrentScopes()
    {
        var scopes = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var scope = _scope.Value; scope is not null; scope = scope.Parent)
        {
            if (scope.State is not IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                continue;
            }

            // Se recorre del más interno al más externo: el interno gana si repiten clave.
            foreach (var pair in pairs)
            {
                scopes.TryAdd(pair.Key, pair.Value);
            }
        }

        return scopes;
    }

    private sealed record Scope(object State, Scope? Parent);

    private sealed class ScopeEnd(RecordingLogger<T> logger, Scope? previous) : IDisposable
    {
        public void Dispose() => logger._scope.Value = previous;
    }
}
