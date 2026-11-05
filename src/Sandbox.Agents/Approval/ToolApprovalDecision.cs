namespace Sandbox.Agents.Approval;

/// <summary>Decisión sobre una acción: si se aprueba, quién decidió (para la auditoría) y, si lo hay, el motivo.</summary>
public sealed record ToolApprovalDecision(bool Approved, string Approver, string? Reason = null)
{
    public static ToolApprovalDecision Approve(string approver) => new(Approved: true, approver);

    public static ToolApprovalDecision Deny(string approver, string? reason = null) => new(Approved: false, approver, reason);
}
