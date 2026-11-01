using System.Diagnostics.CodeAnalysis;

namespace Sandbox.Domain.Results;

/// <summary>
/// Resultado de una operación de dominio: un valor o un <see cref="DomainError"/>, nunca los dos. Las reglas de negocio
/// devuelven este tipo en lugar de lanzar excepciones (ADR 0003).
/// </summary>
public sealed record Result<T>
{
    private Result(T? value, DomainError? error, bool isSuccess)
    {
        Value = value;
        Error = error;
        IsSuccess = isSuccess;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>El valor si la operación tuvo éxito; si no, el valor por defecto.</summary>
    public T? Value { get; }

    /// <summary>El error si la operación falló; si no, <see langword="null"/>.</summary>
    public DomainError? Error { get; }

    internal static Result<T> FromValue(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null, isSuccess: true);
    }

    internal static Result<T> FromError(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error, isSuccess: false);
    }
}
