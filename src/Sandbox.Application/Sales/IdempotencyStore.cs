using Sandbox.Domain.Results;

namespace Sandbox.Application.Sales;

/// <summary>
/// Recuerda los commands que tuvieron éxito, por ámbito y clave de idempotencia (design D6 de <c>add-order-domain</c> y
/// D3 de <c>add-agent-guardrails</c>). Solo se usa con el cerrojo de <see cref="SalesState"/> tomado, para que comprobar
/// la clave y ejecutar sean atómicos.
/// </summary>
internal sealed class IdempotencyStore
{
    private readonly Dictionary<(IdempotencyScope Scope, string Key), Entry> _entries = [];

    /// <summary>
    /// Si la clave ya se usó en ese ámbito, devuelve el resultado que corresponde: el guardado si coinciden la operación
    /// y los argumentos, o <c>IDEMPOTENCY_KEY_CONFLICT</c> si no. Si es nueva en el ámbito, devuelve <see langword="null"/>.
    /// </summary>
    public Result<OrderView>? Replay(IdempotencyScope scope, string key, string operation, IReadOnlyList<string> arguments)
    {
        if (!_entries.TryGetValue((scope, key), out var entry))
        {
            return null;
        }

        var sameRequest = entry.Operation == operation && entry.Arguments.SequenceEqual(arguments, StringComparer.Ordinal);
        return sameRequest ? Result.Success(entry.Result) : Result.Failure<OrderView>(OperationErrors.IdempotencyKeyConflict());
    }

    public void Remember(
        IdempotencyScope scope,
        string key,
        string operation,
        IReadOnlyList<string> arguments,
        OrderView result) =>
        _entries.Add((scope, key), new Entry(operation, arguments, result));

    private sealed record Entry(string Operation, IReadOnlyList<string> Arguments, OrderView Result);
}
