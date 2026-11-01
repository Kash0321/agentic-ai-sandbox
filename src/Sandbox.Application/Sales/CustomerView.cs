using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

public sealed record CustomerView(string CustomerId, string Name, CustomerStatus Status)
{
    internal static CustomerView From(Customer customer) => new(customer.Id, customer.Name, customer.Status);
}
