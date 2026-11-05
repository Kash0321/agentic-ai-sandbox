using Sandbox.Agents.Approval;

namespace Sandbox.Agents.Tests.Fakes;

/// <summary>
/// Aprobador con guion: devuelve las decisiones programadas en orden y guarda las peticiones recibidas, para comprobar
/// qué acción exacta se le preguntó. Se enlaza también en <c>Sandbox.Scenarios.Tests</c>.
/// </summary>
internal sealed class ScriptedToolApprover(params bool[] decisions) : IToolApprover
{
    public const string Name = "Scripted";

    private readonly Queue<bool> _decisions = new(decisions);
    private readonly List<ToolApprovalRequest> _requests = [];

    public IReadOnlyList<ToolApprovalRequest> Requests => _requests;

    /// <summary>Aprobador que siempre deniega, para los tests en los que no debería preguntarse nada.</summary>
    public static ScriptedToolApprover None() => new();

    public Task<ToolApprovalDecision> DecideAsync(ToolApprovalRequest request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        if (!_decisions.TryDequeue(out var approved))
        {
            throw new InvalidOperationException($"El guion del aprobador se ha agotado al preguntar por {request.ToolName}.");
        }

        return Task.FromResult(approved
            ? ToolApprovalDecision.Approve(Name)
            : ToolApprovalDecision.Deny(Name, "denegado por el guion"));
    }
}
