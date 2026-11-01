using Sandbox.Domain.Results;

namespace Sandbox.Application.Sales;

/// <summary>
/// Recuerda los commands que tuvieron éxito, por clave de idempotencia (design D6). Solo se usa con el cerrojo de
/// <see cref="SalesState"/> tomado, para que comprobar la clave y ejecutar sean atómicos.
/// </summary>
internal sealed class IdempotencyStore
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Si la clave ya se usó, devuelve el resultado que corresponde: el guardado si coinciden la operación y los
    /// argumentos, o <c>IDEMPOTENCY_KEY_CONFLICT</c> si no. Si es nueva, devuelve <see langword="null"/>.
    /// </summary>
    public Result<OrderView>? Replay(string key, string operation, IReadOnlyList<string> arguments)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        var sameRequest = entry.Operation == operation && entry.Arguments.SequenceEqual(arguments, StringComparer.Ordinal);
        return sameRequest ? Result.Success(entry.Result) : Result.Failure<OrderView>(OperationErrors.IdempotencyKeyConflict());
    }

    public void Remember(string key, string operation, IReadOnlyList<string> arguments, OrderView result) =>
        _entries.Add(key, new Entry(operation, arguments, result));

    private sealed record Entry(string Operation, IReadOnlyList<string> Arguments, OrderView Result);
}
