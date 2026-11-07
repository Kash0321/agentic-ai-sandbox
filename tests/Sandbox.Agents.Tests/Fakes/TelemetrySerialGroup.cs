namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Los oyentes de spans son globales al proceso: los tests que usan <see cref="SpanRecorder"/> se ejecutan sin
/// paralelismo para no recoger los spans de otro test (design D11 de <c>add-agent-observability</c>).
/// </summary>
[CollectionDefinition(nameof(TelemetrySerialGroup), DisableParallelization = true)]
public sealed class TelemetrySerialGroup;
