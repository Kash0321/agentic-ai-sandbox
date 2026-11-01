namespace Sandbox.Domain.Results;

/// <summary>
/// Error de negocio o de validación: un código estable (<see cref="ErrorCodes"/>), un mensaje breve y accionable sin
/// detalles internos, y su tipo.
/// </summary>
public sealed record DomainError(string Code, string Message, ErrorKind Kind);
