namespace Sandbox.Application.Sales;

/// <summary>
/// Ámbito en el que se registra una clave de idempotencia (design D3 de <c>add-agent-guardrails</c>): el cliente que
/// pide la operación, o el almacén. La misma clave en dos ámbitos son dos claves independientes. Es un tipo y no un
/// prefijo de texto para que un identificador de cliente no pueda coincidir con el del almacén.
/// </summary>
internal readonly record struct IdempotencyScope
{
    private IdempotencyScope(bool isWarehouse, string customerId)
    {
        IsWarehouse = isWarehouse;
        CustomerId = customerId;
    }

    public static IdempotencyScope Warehouse { get; } = new(isWarehouse: true, string.Empty);

    public bool IsWarehouse { get; }

    public string CustomerId { get; }

    public static IdempotencyScope ForCustomer(string customerId) => new(isWarehouse: false, customerId);
}
