namespace Sandbox.Agents.Approval;

/// <summary>La acción exacta que se va a confirmar: la tool y los argumentos con los que se ejecutará si se aprueba.</summary>
public sealed record ToolApprovalRequest(string ToolName, IReadOnlyDictionary<string, object?> Arguments);
