using Sandbox.Domain.Results;

namespace Sandbox.Application.Sales;

/// <summary>Errores propios de las operaciones de aplicación, que no son reglas del dominio.</summary>
public static class OperationErrors
{
    public static DomainError InvalidIdempotencyKey() =>
        new(
            ErrorCodes.InvalidIdempotencyKey,
            $"La clave de idempotencia es obligatoria: de 1 a {IdempotencyKey.MaxLength} caracteres, sin caracteres de control.",
            ErrorKind.Validation);

    public static DomainError IdempotencyKeyConflict() =>
        new(
            ErrorCodes.IdempotencyKeyConflict,
            "Esa clave de idempotencia ya se usó con otra operación o con otros argumentos; usa una clave nueva.",
            ErrorKind.Conflict);

    public static DomainError ServiceUnavailable() =>
        new(
            ErrorCodes.ServiceUnavailable,
            "El servicio no está disponible en este momento; puedes reintentar la misma petición.",
            ErrorKind.Transient);
}
