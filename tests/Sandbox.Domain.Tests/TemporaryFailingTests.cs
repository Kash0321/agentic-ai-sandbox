namespace Sandbox.Domain.Tests;

// Prueba negativa temporal de add-ci-pipeline (tarea 3.4): se revierte en el commit siguiente.
public sealed class TemporaryFailingTests
{
    [Fact]
    public void Ci_WithFailingTest_TurnsCheckRed()
    {
        Assert.Equal(1, 2);
    }
}
