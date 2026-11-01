using Microsoft.Extensions.Logging;

namespace Sandbox.Application.Tests.Fakes;

internal sealed record RecordedLog(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties);
