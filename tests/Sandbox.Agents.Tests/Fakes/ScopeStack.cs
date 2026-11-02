namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Pila de <i>scopes</i> de registro que sigue el flujo asíncrono (<see cref="AsyncLocal{T}"/>), para los dobles de
/// registro usados fuera de un host. Se enlaza también en <c>Sandbox.Scenarios.Tests</c>.
/// </summary>
internal sealed class ScopeStack
{
    private readonly AsyncLocal<Node?> _top = new();

    public IDisposable Push(object state)
    {
        var previous = _top.Value;
        _top.Value = new Node(state, previous);
        return new Pop(this, previous);
    }

    /// <summary>Copia las propiedades de los scopes activos, del más externo al más interno.</summary>
    public void CopyTo(Dictionary<string, object?> target)
    {
        Stack<object> states = new();
        for (var node = _top.Value; node is not null; node = node.Parent)
        {
            states.Push(node.State);
        }

        foreach (var state in states)
        {
            Merge(state, target);
        }
    }

    public static void Merge(object? scope, Dictionary<string, object?> target)
    {
        if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
            {
                target[pair.Key] = pair.Value;
            }
        }
    }

    private sealed record Node(object State, Node? Parent);

    private sealed class Pop(ScopeStack stack, Node? previous) : IDisposable
    {
        public void Dispose() => stack._top.Value = previous;
    }
}
