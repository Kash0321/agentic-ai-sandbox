using System.Reflection;

namespace Sandbox.Agents.Tests;

// Test de cableado temporal: se sustituye por tests reales de las tools en la fase 4.
public sealed class AgentsAssemblyWiringTests
{
    [Fact]
    public void Load_AgentsAssembly_HasExpectedName()
    {
        var assembly = Assembly.Load("Sandbox.Agents");

        Assert.Equal("Sandbox.Agents", assembly.GetName().Name);
    }
}
