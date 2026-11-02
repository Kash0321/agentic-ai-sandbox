using Sandbox.Domain.Results;

namespace Sandbox.Agents.Tools;

/// <summary>
/// Error que recibe el modelo: un código estable, un mensaje breve y accionable, y si repetir la misma petición puede
/// funcionar (<c>AGENTS.md</c>, sección 4.4).
/// </summary>
public sealed record ToolError(string Code, string Message, bool Retryable)
{
    /// <summary>Traduce un error de dominio: solo los transitorios se pueden reintentar (ADR 0003).</summary>
    public static ToolError From(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ToolError(error.Code, error.Message, error.Kind == ErrorKind.Transient);
    }

    /// <summary>Fallo inesperado: el detalle queda en el registro, nunca en el mensaje.</summary>
    public static ToolError Internal() =>
        new(
            ToolErrorCodes.InternalError,
            "Error interno al ejecutar la herramienta. No repitas la petición; informa al cliente.",
            Retryable: false);

    /// <summary>Argumentos que no se pueden asignar a los parámetros de la tool.</summary>
    public static ToolError InvalidArguments(string toolName) =>
        new(
            ToolErrorCodes.InvalidArguments,
            $"Los argumentos de {toolName} no cumplen su esquema: revisa los parámetros obligatorios y sus tipos.",
            Retryable: false);
}
