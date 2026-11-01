namespace Sandbox.Scenarios.Tests.Runner;

/// <summary>
/// Los tests del host leen variables de entorno, que son globales al proceso: se ejecutan en serie para que un test
/// que define <c>DeepSeek__ApiKey</c> no afecte a otro que espera que no haya clave.
/// </summary>
[CollectionDefinition(nameof(RunnerHostSerialGroup), DisableParallelization = true)]
public sealed class RunnerHostSerialGroup;
