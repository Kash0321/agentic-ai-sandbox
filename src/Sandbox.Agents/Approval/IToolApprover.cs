namespace Sandbox.Agents.Approval;

/// <summary>
/// Quien decide si se ejecuta una acción que requiere confirmación (spec <c>tool-approval</c>, design D5). Lo
/// implementa el anfitrión: el agente no sabe si la decisión viene de una consola, de una interfaz o de un guion.
/// </summary>
public interface IToolApprover
{
    Task<ToolApprovalDecision> DecideAsync(ToolApprovalRequest request, CancellationToken cancellationToken);
}
