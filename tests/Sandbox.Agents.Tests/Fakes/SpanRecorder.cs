using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Oyente de spans en memoria para los tests: muestrea todo lo que emiten las fuentes indicadas y guarda los spans que
/// terminan mientras está vivo. El oyente es global al proceso, así que los tests que lo usan van en una colección sin
/// paralelismo: <c>TelemetrySerialGroup</c> aquí y <c>RunnerHostSerialGroup</c> en <c>Sandbox.Scenarios.Tests</c>, donde
/// también se enlaza (design D11 de <c>add-agent-observability</c>).
/// </summary>
internal sealed class SpanRecorder : IDisposable
{
    private readonly HashSet<string> _sources;
    private readonly ConcurrentQueue<Activity> _spans = new();
    private readonly ActivityListener _listener;

    public SpanRecorder(params string[] sourceNames)
    {
        _sources = new HashSet<string>(sourceNames, StringComparer.Ordinal);
        _listener = new ActivityListener
        {
            ShouldListenTo = source => _sources.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _spans.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>Spans terminados, en el orden en que terminaron.</summary>
    public IReadOnlyList<Activity> Spans => [.. _spans];

    /// <summary>Spans terminados cuyo nombre visible empieza por <paramref name="prefix"/>.</summary>
    public IReadOnlyList<Activity> Named(string prefix) =>
        [.. _spans.Where(span => span.DisplayName.StartsWith(prefix, StringComparison.Ordinal))];

    /// <summary>Todo el texto que lleva un span: estado, atributos y eventos con sus atributos.</summary>
    public static string TextOf(Activity span)
    {
        ArgumentNullException.ThrowIfNull(span);
        var tags = span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}");
        var events = span.Events.SelectMany(spanEvent =>
            spanEvent.Tags.Select(tag => $"{tag.Key}={tag.Value}").Prepend(spanEvent.Name));
        return string.Join("\n", tags.Concat(events).Prepend(span.StatusDescription ?? string.Empty));
    }

    public void Dispose() => _listener.Dispose();
}
