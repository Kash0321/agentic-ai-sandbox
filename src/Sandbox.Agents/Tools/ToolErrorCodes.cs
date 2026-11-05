namespace Sandbox.Agents.Tools;

/// <summary>Códigos que nacen en la capa de tools; los del dominio están en <c>ErrorCodes</c>.</summary>
public static class ToolErrorCodes
{
    public const string InternalError = "INTERNAL_ERROR";

    public const string InvalidArguments = "INVALID_ARGUMENTS";

    /// <summary>La allow-list impidió ejecutar una tool del catálogo (spec <c>agent-guardrails</c>).</summary>
    public const string BlockedByPolicy = "BLOCKED_BY_POLICY";

    /// <summary>
    /// El modelo pidió una tool que no existe. No es un error que reciba el modelo, sino el resultado con que se
    /// registra esa petición en la ejecución y en la auditoría.
    /// </summary>
    public const string UnknownTool = "UNKNOWN_TOOL";

    /// <summary>
    /// Una persona denegó una acción que requiere confirmación (spec <c>tool-approval</c>). Es el resultado con que se
    /// registra esa petición; el modelo recibe el aviso de la librería con el motivo.
    /// </summary>
    public const string ConfirmationDenied = "CONFIRMATION_DENIED";
}
