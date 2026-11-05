using Sandbox.Agents.Guardrails;
using Sandbox.Agents.Tools;

namespace Sandbox.Agents.Tests.Guardrails;

public sealed class GuardrailOptionsValidatorTests
{
    private readonly GuardrailOptionsValidator _validator = new();

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var options = new GuardrailOptions();

        Assert.True(_validator.Validate(null, options).Succeeded);
        Assert.Equal(10, options.MaxModelCalls);
        Assert.Equal(3, options.MaxConsecutiveToolErrors);
        Assert.Equal(20_000, options.TokenBudget);
    }

    [Theory]
    [InlineData(nameof(GuardrailOptions.MaxModelCalls))]
    [InlineData(nameof(GuardrailOptions.MaxConsecutiveToolErrors))]
    [InlineData(nameof(GuardrailOptions.TokenBudget))]
    public void Validate_LimitBelowOne_FailsNamingTheSetting(string property)
    {
        var options = new GuardrailOptions();
        typeof(GuardrailOptions).GetProperty(property)?.SetValue(options, 0);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"Guardrails:{property}", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EmptyAllowedTools_FailsNamingTheSetting()
    {
        var options = new GuardrailOptions { AllowedTools = [] };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Guardrails:AllowedTools", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AllowedToolThatDoesNotExist_FailsNamingTheSettingAndTheTool()
    {
        var options = new GuardrailOptions { AllowedTools = ["list_products", "ship_order"] };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Guardrails:AllowedTools", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("ship_order", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SubsetOfTheCatalog_Succeeds()
    {
        var options = new GuardrailOptions { AllowedTools = ["list_products", "get_stock"] };

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void EffectiveAllowedTools_WithoutAllowedTools_IsTheWholeCatalog()
    {
        Assert.Equal(SalesToolNames.All, new GuardrailOptions().EffectiveAllowedTools);
    }

    [Fact]
    public void EffectiveAllowedTools_WithAllowedTools_IsThatList()
    {
        var options = new GuardrailOptions { AllowedTools = ["get_stock"] };

        Assert.Equal(["get_stock"], options.EffectiveAllowedTools);
    }
}
