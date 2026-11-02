using Sandbox.Domain.Results;

namespace Sandbox.Agents.Tools;

/// <summary>Fábricas de <see cref="ToolResult{T}"/>.</summary>
public static class ToolResult
{
    public static ToolResult<T> Success<T>(T data) => new(true, data, null);

    public static ToolResult<T> Failure<T>(ToolError error) => new(false, default, error);

    /// <summary>Traduce un resultado de la aplicación sin añadir ninguna regla.</summary>
    public static ToolResult<T> From<T>(Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? Success(result.Value) : Failure<T>(ToolError.From(result.Error));
    }
}
