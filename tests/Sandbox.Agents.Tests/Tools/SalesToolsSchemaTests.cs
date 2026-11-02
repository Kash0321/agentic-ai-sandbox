using System.Text.Json;
using System.Text.RegularExpressions;
using Sandbox.Agents.Tools;

namespace Sandbox.Agents.Tests.Tools;

public sealed partial class SalesToolsSchemaTests : IDisposable
{
    private readonly ToolHarness _harness = new();

    [Fact]
    public void CreateFunctions_Always_ExposesExactlyTheSixStableNames()
    {
        Assert.Equal(
            ["list_products", "get_stock", "get_customer", "get_order", "place_order", "cancel_order"],
            _harness.Tools.CreateFunctions().Select(function => function.Name));
    }

    [Theory]
    [InlineData(SalesToolNames.ListProducts, new string[0])]
    [InlineData(SalesToolNames.GetStock, new[] { "sku" })]
    [InlineData(SalesToolNames.GetCustomer, new string[0])]
    [InlineData(SalesToolNames.GetOrder, new[] { "orderId" })]
    [InlineData(SalesToolNames.PlaceOrder, new[] { "lines", "idempotencyKey" })]
    [InlineData(SalesToolNames.CancelOrder, new[] { "orderId", "idempotencyKey" })]
    public void JsonSchema_OfEachTool_DeclaresExactlyItsRequiredParameters(string toolName, string[] parameters)
    {
        var schema = _harness.Functions[toolName].JsonSchema;

        Assert.Equal(parameters, schema.GetProperty("properties").EnumerateObject().Select(property => property.Name));
        var required = schema.TryGetProperty("required", out var list) ? list.EnumerateArray().Select(item => item.GetString()) : [];
        Assert.Equal(parameters, required);
    }

    [Fact]
    public void JsonSchema_OfPlaceOrder_DescribesTheLinesAsObjectsWithSkuAndQuantity()
    {
        var lines = _harness.Functions[SalesToolNames.PlaceOrder].JsonSchema.GetProperty("properties").GetProperty("lines");

        Assert.Equal("array", lines.GetProperty("type").GetString());
        var item = lines.GetProperty("items");
        Assert.Equal(["sku", "quantity"], item.GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.Equal(["sku", "quantity"], item.GetProperty("required").EnumerateArray().Select(entry => entry.GetString()));
    }

    [Fact]
    public void Descriptions_OfEveryToolAndParameter_AreNotEmpty()
    {
        foreach (var function in _harness.Functions.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(function.Description), function.Name);
            Assert.All(Descriptions(function.JsonSchema), description => Assert.False(string.IsNullOrWhiteSpace(description)));
            Assert.All(
                function.JsonSchema.GetProperty("properties").EnumerateObject(),
                property => Assert.True(property.Value.TryGetProperty("description", out _), $"{function.Name}.{property.Name}"));
        }
    }

    [Theory]
    [InlineData(SalesToolNames.PlaceOrder)]
    [InlineData(SalesToolNames.CancelOrder)]
    public void IdempotencyKeyDescription_OfCommands_AsksForANewKeyAndTheSameOnRetry(string toolName)
    {
        var description = _harness.Functions[toolName].JsonSchema
            .GetProperty("properties").GetProperty("idempotencyKey").GetProperty("description").GetString();

        Assert.Contains("nueva para cada petición distinta", description, StringComparison.Ordinal);
        Assert.Contains("exactamente la misma clave", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Descriptions_WhenTheyCiteATool_UseAnExistingName()
    {
        var texts = _harness.Functions.Values.SelectMany(function => Descriptions(function.JsonSchema).Prepend(function.Description));

        var cited = texts.SelectMany(text => SnakeCaseWord().Matches(text).Select(match => match.Value)).Distinct();

        Assert.All(cited, name => Assert.Contains(name, SalesToolNames.All));
    }

    [Fact]
    public void JsonSchema_OfAnyTool_HasNoCustomerOrCancellationParameter()
    {
        var parameters = _harness.Functions.Values
            .SelectMany(function => function.JsonSchema.GetProperty("properties").EnumerateObject())
            .Select(property => property.Name);

        Assert.DoesNotContain(parameters, name => name.Contains("customer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parameters, name => name.Contains("cancellation", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose() => _harness.Dispose();

    private static IEnumerable<string> Descriptions(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name == "description" && property.Value.ValueKind == JsonValueKind.String)
                {
                    yield return property.Value.GetString() ?? string.Empty;
                }

                foreach (var nested in Descriptions(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var nested in element.EnumerateArray().SelectMany(Descriptions))
            {
                yield return nested;
            }
        }
    }

    [GeneratedRegex(@"\b[a-z]+(?:_[a-z]+)+\b")]
    private static partial Regex SnakeCaseWord();
}
