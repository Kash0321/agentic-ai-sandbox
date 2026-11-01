namespace Sandbox.Domain.Sales;

/// <summary>Cliente del dominio de ventas. Un cliente bloqueado no puede hacer pedidos.</summary>
public sealed class Customer(string id, string name, CustomerStatus status)
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public CustomerStatus Status { get; } = status;
}
