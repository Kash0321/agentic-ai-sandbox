using System.Reflection;

namespace Sandbox.Domain.Tests;

// Test de cableado temporal: se sustituye por tests reales del dominio en la fase 3.
public sealed class DomainAssemblyWiringTests
{
    [Fact]
    public void Load_DomainAssembly_HasExpectedName()
    {
        var assembly = Assembly.Load("Sandbox.Domain");

        Assert.Equal("Sandbox.Domain", assembly.GetName().Name);
    }
}
