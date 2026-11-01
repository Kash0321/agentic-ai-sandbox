using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Application.Tests.Sales;

public sealed class SeedCatalogTests
{
    private static readonly string[] SeedCustomerIds = ["CUST-001", "CUST-002", "CUST-003"];
    private static readonly string[] SeedSkus = ["SKU-MUG-RED", "SKU-TSHIRT-M", "SKU-LAMP-DESK", "SKU-CHAIR-ERGO"];

    [Fact]
    public async Task CreateState_Twice_GivesTheSameCustomersAndProducts()
    {
        var first = new SalesQueries(SeedCatalog.CreateState());
        var second = new SalesQueries(SeedCatalog.CreateState());

        Assert.Equal(await CustomersOf(first), await CustomersOf(second));
        Assert.Equal(await ProductsOf(first), await ProductsOf(second));
    }

    [Fact]
    public async Task CreateState_Always_HasABlockedCustomerAndAProductWithoutStock()
    {
        var queries = new SalesQueries(SeedCatalog.CreateState());

        Assert.Contains(await CustomersOf(queries), customer => customer.Status == CustomerStatus.Blocked);
        Assert.Contains(await ProductsOf(queries), product => product.Available == 0);
    }

    [Fact]
    public async Task SalesState_BuiltByTheTest_ReplacesTheCatalog()
    {
        var harness = new SalesHarness(SalesHarness.StateWith(SalesHarness.Product("SKU-TEST")));

        Assert.Equal(20, await harness.AvailableAsync("SKU-TEST"));
        var seedCustomer = await harness.Queries.GetCustomerAsync(new GetCustomerQuery("CUST-001"), TestContext.Current.CancellationToken);
        ResultAssert.Fails(seedCustomer, ErrorCodes.CustomerNotFound);
    }

    private static async Task<List<CustomerView>> CustomersOf(SalesQueries queries)
    {
        List<CustomerView> customers = [];
        foreach (var id in SeedCustomerIds)
        {
            var result = await queries.GetCustomerAsync(new GetCustomerQuery(id), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess, id);
            customers.Add(result.Value);
        }

        return customers;
    }

    private static async Task<List<StockView>> ProductsOf(SalesQueries queries)
    {
        List<StockView> products = [];
        foreach (var sku in SeedSkus)
        {
            var result = await queries.GetStockAsync(new GetStockQuery(sku), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess, sku);
            products.Add(result.Value);
        }

        return products;
    }
}
