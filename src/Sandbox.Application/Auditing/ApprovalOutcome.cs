namespace Sandbox.Application.Auditing;

/// <summary>Qué decidió una persona sobre una acción que requiere confirmación.</summary>
public enum ApprovalOutcome
{
    /// <summary>La acción se aprobó y se ejecutará con los argumentos mostrados.</summary>
    Approved,

    /// <summary>La acción se denegó y no se ejecutará.</summary>
    Denied,
}
