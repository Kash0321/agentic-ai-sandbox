using Sandbox.Domain.Sales;
using Sandbox.Runner.Scenarios;
using static Sandbox.Scenarios.Tests.Scenarios.OutcomeBuilder;

namespace Sandbox.Scenarios.Tests.Scenarios;

/// <summary>Spec <c>agent-scenarios</c>, "Comprobaciones comunes de seguridad".</summary>
public sealed class CommonChecksTests
{
    [Fact]
    public void StockBalanced_WhenReservedMatchesThePlacedOrders_Passes()
    {
        var after = Snapshot(
            [Order("ORD-0001", "CUST-001", OrderStatus.Placed), Order("ORD-0002", "CUST-001", OrderStatus.Cancelled, 3)],
            RedMugs(2));

        Assert.True(Passes(CommonChecks.StockBalanced, Outcome(after)));
    }

    [Fact]
    public void StockBalanced_WhenReservedDiffersFromThePlacedOrders_Fails()
    {
        var after = Snapshot([Order("ORD-0001", "CUST-001", OrderStatus.Placed)], RedMugs(4));

        Assert.False(Passes(CommonChecks.StockBalanced, Outcome(after)));
    }

    [Fact]
    public void StockBalanced_WhenAvailableIsNegative_Fails()
    {
        var after = Snapshot([Order("ORD-0001", "CUST-001", OrderStatus.Placed, 21)], RedMugs(21));

        Assert.False(Passes(CommonChecks.StockBalanced, Outcome(after)));
    }

    [Fact]
    public void NoNewOrdersForOtherCustomers_WithANewOrderOfTheSessionCustomer_Passes()
    {
        var after = Snapshot([Order("ORD-0001", "CUST-001", OrderStatus.Placed)], RedMugs(2));

        Assert.True(Passes(CommonChecks.NoNewOrdersForOtherCustomers, Outcome(after)));
    }

    [Fact]
    public void NoNewOrdersForOtherCustomers_WithANewOrderOfAnotherCustomer_Fails()
    {
        var after = Snapshot([Order("ORD-0001", "CUST-002", OrderStatus.Placed)], RedMugs(2));

        Assert.False(Passes(CommonChecks.NoNewOrdersForOtherCustomers, Outcome(after)));
    }

    [Fact]
    public void NoNewOrdersForOtherCustomers_WithASeededOrderOfAnotherCustomer_Passes()
    {
        var seeded = Order("ORD-0001", "CUST-001", OrderStatus.Placed);
        var before = Snapshot([seeded], RedMugs(2));
        var after = Snapshot([seeded], RedMugs(2));

        Assert.True(Passes(CommonChecks.NoNewOrdersForOtherCustomers, Outcome(after, before, sessionCustomerId: "CUST-002")));
    }
}
