using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sandbox.Application.Sales;
using Sandbox.Domain.Sales;
using Sandbox.Runner;

namespace Sandbox.Scenarios.Tests.Runner;

[Collection(nameof(RunnerHostSerialGroup))]
public sealed class RunnerApplicationServicesTests
{
    [Fact]
    public async Task CreateBuilder_WithDefaultSettings_RegistersCommandsAndQueriesOverTheSeedCatalog()
    {
        var token = TestContext.Current.CancellationToken;
        using var host = RunnerHost.CreateBuilder(["--DeepSeek:ApiKey=sk-test"], TemporarySecretsDirectory.Missing()).Build();

        await host.StartAsync(token);
        var commands = host.Services.GetRequiredService<ISalesCommands>();
        var customer = await host.Services.GetRequiredService<ISalesQueries>()
            .GetCustomerAsync(new GetCustomerQuery("CUST-003"), token);
        await host.StopAsync(token);

        Assert.NotNull(commands);
        Assert.Equal(new CustomerView("CUST-003", "Carla Demo", CustomerStatus.Blocked), customer.Value);
    }

    [Fact]
    public async Task StartAsync_WithTwoInvalidOptionTypes_ThrowsAggregateOfOptionsValidationExceptions()
    {
        using var host = RunnerHost.CreateBuilder(
            ["--FaultInjection:Rules:0:Operation=DeleteEverything", "--FaultInjection:Rules:0:Trigger=Always"],
            TemporarySecretsDirectory.Missing()).Build();

        var exception = await Record.ExceptionAsync(() => host.StartAsync(TestContext.Current.CancellationToken));

        var aggregate = Assert.IsType<AggregateException>(exception);
        Assert.All(aggregate.InnerExceptions, inner => Assert.IsType<OptionsValidationException>(inner));
        Assert.Equal(2, aggregate.InnerExceptions.Count);
    }
}
