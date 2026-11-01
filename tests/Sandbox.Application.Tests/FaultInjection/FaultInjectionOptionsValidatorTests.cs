using Microsoft.Extensions.Options;
using Sandbox.Application.FaultInjection;
using static Sandbox.Application.Tests.FaultInjection.FaultHarness;

namespace Sandbox.Application.Tests.FaultInjection;

public sealed class FaultInjectionOptionsValidatorTests
{
    private readonly FaultInjectionOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithoutRules_Succeeds()
    {
        var result = _validator.Validate(null, new FaultInjectionOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithTheDesignExample_Succeeds()
    {
        var result = Validate(
            Rule(FaultOperations.PlaceOrder, FaultTrigger.FirstCalls, count: 1, timing: FaultTiming.After),
            Rule(FaultOperations.GetStock, FaultTrigger.Probability, probability: 0.3),
            Rule(FaultOperations.All, latency: TimeSpan.FromMilliseconds(200)));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithUnknownOperation_FailsNamingTheRuleOperation()
    {
        var result = Validate(Rule("DeleteEverything", FaultTrigger.Always));

        AssertSingleFailureAbout(result, "FaultInjection:Rules:0:Operation");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_FirstCallsWithoutAValidCount_FailsNamingTheCount(int? count)
    {
        var result = Validate(Rule(FaultOperations.GetOrder, FaultTrigger.FirstCalls, count: count));

        AssertSingleFailureAbout(result, "FaultInjection:Rules:0:Count");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void Validate_ProbabilityOutOfRange_FailsNamingTheProbability(double? probability)
    {
        var result = Validate(Rule(FaultOperations.GetStock, FaultTrigger.Probability, probability: probability));

        AssertSingleFailureAbout(result, "FaultInjection:Rules:0:Probability");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31_000)]
    public void Validate_LatencyOutOfRange_FailsNamingTheLatency(int milliseconds)
    {
        var result = Validate(Rule(FaultOperations.GetOrder, FaultTrigger.Always, latency: TimeSpan.FromMilliseconds(milliseconds)));

        AssertSingleFailureAbout(result, "FaultInjection:Rules:0:Latency");
    }

    [Fact]
    public void Validate_LatencyOfExactly30Seconds_Succeeds()
    {
        var result = Validate(Rule(FaultOperations.GetOrder, latency: TimeSpan.FromSeconds(30)));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_RuleThatNeitherFailsNorDelays_FailsNamingTheTrigger()
    {
        var result = Validate(Rule(FaultOperations.GetOrder));

        AssertSingleFailureAbout(result, "FaultInjection:Rules:0:Trigger");
    }

    [Fact]
    public void Validate_InvalidSecondRule_NamesItsIndex()
    {
        var result = Validate(
            Rule(FaultOperations.GetOrder, FaultTrigger.Always),
            Rule(FaultOperations.GetStock, FaultTrigger.Probability, probability: 1.5));

        AssertSingleFailureAbout(result, "FaultInjection:Rules:1:Probability");
    }

    private ValidateOptionsResult Validate(params FaultRule[] rules)
    {
        var options = new FaultInjectionOptions { Seed = DefaultSeed };
        foreach (var rule in rules)
        {
            options.Rules.Add(rule);
        }

        return _validator.Validate(null, options);
    }

    private static void AssertSingleFailureAbout(ValidateOptionsResult result, string settingPath)
    {
        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith(settingPath, failure, StringComparison.Ordinal);
    }
}
