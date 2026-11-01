namespace Sandbox.Domain.Results;

/// <summary>Fábricas de <see cref="Result{T}"/>, para no repetir el argumento de tipo cuando se puede inferir.</summary>
public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.FromValue(value);

    public static Result<T> Failure<T>(DomainError error) => Result<T>.FromError(error);
}
