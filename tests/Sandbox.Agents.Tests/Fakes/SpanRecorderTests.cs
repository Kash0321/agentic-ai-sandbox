using System.Diagnostics;

namespace Sandbox.Agents.Tests.Fakes;

[Collection(nameof(TelemetrySerialGroup))]
public sealed class SpanRecorderTests
{
    [Fact]
    public void Spans_FromAListenedSource_AreRecordedWithTheirTags()
    {
        using var source = new ActivitySource("SpanRecorderTests.Listened");
        using var recorder = new SpanRecorder(source.Name);

        using (var span = source.StartActivity("unidad"))
        {
            span?.SetTag("clave", "valor");
        }

        var recorded = Assert.Single(recorder.Named("unidad"));
        Assert.Equal("valor", recorded.GetTagItem("clave"));
    }

    [Fact]
    public void Spans_FromAnotherSource_AreIgnored()
    {
        using var listened = new ActivitySource("SpanRecorderTests.Listened");
        using var other = new ActivitySource("SpanRecorderTests.Other");
        using var recorder = new SpanRecorder(listened.Name);

        using (other.StartActivity("ajeno"))
        {
        }

        Assert.Empty(recorder.Spans);
    }

    [Fact]
    public void Spans_EndingAfterDispose_AreNotRecorded()
    {
        using var source = new ActivitySource("SpanRecorderTests.Listened");
        var recorder = new SpanRecorder(source.Name);
        var span = source.StartActivity("tardío");

        recorder.Dispose();
        span?.Dispose();

        Assert.Empty(recorder.Spans);
    }

    [Fact]
    public void TextOf_WithStatusTagsAndEvents_IncludesAllOfThem()
    {
        using var source = new ActivitySource("SpanRecorderTests.Listened");
        using var recorder = new SpanRecorder(source.Name);

        using (var span = source.StartActivity("unidad"))
        {
            span?.SetStatus(ActivityStatusCode.Error, "estado");
            span?.SetTag("atributo", "a");
            span?.AddEvent(new ActivityEvent("evento", tags: new ActivityTagsCollection { ["detalle"] = "d" }));
        }

        var text = SpanRecorder.TextOf(Assert.Single(recorder.Spans));
        Assert.Contains("estado", text, StringComparison.Ordinal);
        Assert.Contains("atributo=a", text, StringComparison.Ordinal);
        Assert.Contains("detalle=d", text, StringComparison.Ordinal);
    }
}
