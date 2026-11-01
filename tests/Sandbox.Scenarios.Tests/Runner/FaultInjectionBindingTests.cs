using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sandbox.Application.FaultInjection;
using Sandbox.Runner;

namespace Sandbox.Scenarios.Tests.Runner;

[Collection(nameof(RunnerHostSerialGroup))]
public sealed class FaultInjectionBindingTests
{
    [Fact]
    public void CreateBuilder_WithFaultInjectionSection_BindsEnumsAsTextAndLatencyAsTimeSpan()
    {
        var builder = RunnerHost.CreateBuilder([], TemporarySecretsDirectory.Missing());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FaultInjection:Seed"] = "7",
            ["FaultInjection:Rules:0:Operation"] = "PlaceOrder",
            ["FaultInjection:Rules:0:Trigger"] = "FirstCalls",
            ["FaultInjection:Rules:0:Count"] = "2",
            ["FaultInjection:Rules:0:Timing"] = "After",
            ["FaultInjection:Rules:1:Operation"] = "*",
            ["FaultInjection:Rules:1:Latency"] = "00:00:00.200",
            ["FaultInjection:Rules:2:Operation"] = "GetStock",
            ["FaultInjection:Rules:2:Trigger"] = "Probability",
            ["FaultInjection:Rules:2:Probability"] = "0.3",
        });

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<FaultInjectionOptions>>().Value;

        Assert.Equal(7, options.Seed);
        Assert.Equal(3, options.Rules.Count);
        Assert.Equal(FaultTrigger.FirstCalls, options.Rules[0].Trigger);
        Assert.Equal(2, options.Rules[0].Count);
        Assert.Equal(FaultTiming.After, options.Rules[0].Timing);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.Rules[1].Latency);
        Assert.Equal(FaultTrigger.None, options.Rules[1].Trigger);
        Assert.Equal(0.3, options.Rules[2].Probability);
    }

    [Fact]
    public void CreateBuilder_WithoutFaultInjectionSection_HasNoRules()
    {
        using var host = RunnerHost.CreateBuilder([], TemporarySecretsDirectory.Missing()).Build();

        var options = host.Services.GetRequiredService<IOptions<FaultInjectionOptions>>().Value;

        Assert.Empty(options.Rules);
    }
}
