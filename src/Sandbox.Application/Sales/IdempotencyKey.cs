namespace Sandbox.Application.Sales;

/// <summary>Formato de la clave de idempotencia: de 1 a 64 caracteres, sin caracteres de control ni solo espacios.</summary>
internal static class IdempotencyKey
{
    public const int MaxLength = 64;

    public static bool IsValid(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Length <= MaxLength
        && !key.Any(char.IsControl);
}
