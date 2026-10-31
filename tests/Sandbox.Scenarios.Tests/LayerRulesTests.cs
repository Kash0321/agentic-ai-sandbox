using System.Reflection;

namespace Sandbox.Scenarios.Tests;

public sealed class LayerRulesTests
{
    private static readonly string[] AiLibraryPrefixes =
    [
        "Microsoft.Extensions.AI",
        "OpenAI",
        "Azure.AI",
        "Anthropic",
        "ModelContextProtocol",
    ];

    [Theory]
    [InlineData("Sandbox.Domain")]
    [InlineData("Sandbox.Application")]
    public void ReferencedAssemblies_OfDomainAndApplication_IncludeNoAiLibraries(string assemblyName)
    {
        var aiLibraries = ReferencedAssemblyNames(Assembly.Load(assemblyName))
            .Where(name => AiLibraryPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(aiLibraries);
    }

    [Fact]
    public void ReferencedAssemblies_OfApplication_DoNotIncludeAgentsOrRunner()
    {
        var upperLayers = ReferencedAssemblyNames(Assembly.Load("Sandbox.Application"))
            .Where(name => name is "Sandbox.Agents" or "Sandbox.Runner")
            .ToList();

        Assert.Empty(upperLayers);
    }

    [Fact]
    public void ReferencedAssemblies_OfDomain_DoNotIncludeOtherSandboxAssemblies()
    {
        var otherLayers = ReferencedAssemblyNames(Assembly.Load("Sandbox.Domain"))
            .Where(name => name.StartsWith("Sandbox.", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(otherLayers);
    }

    private static HashSet<string> ReferencedAssemblyNames(Assembly root)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<Assembly>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            foreach (var reference in pending.Pop().GetReferencedAssemblies())
            {
                if (reference.Name is null || !visited.Add(reference.Name) || IsFramework(reference.Name))
                {
                    continue;
                }

                if (TryLoad(reference) is { } loaded)
                {
                    pending.Push(loaded);
                }
            }
        }

        return visited;
    }

    private static bool IsFramework(string name) =>
        name is "netstandard" or "mscorlib" || name.StartsWith("System", StringComparison.Ordinal);

    private static Assembly? TryLoad(AssemblyName name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
