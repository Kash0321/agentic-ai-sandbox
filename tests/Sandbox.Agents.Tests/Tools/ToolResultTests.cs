using System.Text.Json;
using Sandbox.Agents.Tools;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Agents.Tests.Tools;

public sealed class ToolResultTests
{
    [Fact]
    public void From_SuccessfulResult_HasDataAndNoError()
    {
        var result = ToolResult.From(Result.Success("dato"));

        Assert.True(result.Success);
        Assert.Equal("dato", result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void From_FailedResult_HasErrorWithCodeAndMessageAndNoData()
    {
        var result = ToolResult.From(Result.Failure<string>(new DomainError("SOME_CODE", "Mensaje.", ErrorKind.NotFound)));

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(new ToolError("SOME_CODE", "Mensaje.", Retryable: false), result.Error);
    }

    [Theory]
    [InlineData(ErrorKind.Validation, false)]
    [InlineData(ErrorKind.NotFound, false)]
    [InlineData(ErrorKind.BusinessRule, false)]
    [InlineData(ErrorKind.Conflict, false)]
    [InlineData(ErrorKind.Transient, true)]
    public void ToolErrorFrom_EachErrorKind_IsRetryableOnlyWhenTransient(ErrorKind kind, bool expected)
    {
        var error = ToolError.From(new DomainError("CODE", "Mensaje.", kind));

        Assert.Equal(expected, error.Retryable);
    }

    [Fact]
    public void Serialize_SuccessfulOrder_HasStatusAsTextAndNoErrorField()
    {
        var order = new OrderView(
            "ORD-0001",
            "CUST-001",
            OrderStatus.Placed,
            [new OrderLineView("SKU-MUG-RED", 2, 12.50m, 25.00m)],
            25.00m,
            "EUR",
            new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero));

        var json = JsonSerializer.Serialize(ToolResult.Success(order), ToolJson.SerializerOptions);

        Assert.Contains("\"status\":\"Placed\"", json, StringComparison.Ordinal);
        Assert.Contains("\"success\":true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"error\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_TextWithAccents_KeepsThemReadable()
    {
        var product = new StockView("SKU-LAMP-DESK", "Lámpara de escritorio", 89.00m, 0);

        var json = JsonSerializer.Serialize(ToolResult.Success(product), ToolJson.SerializerOptions);

        Assert.Contains("Lámpara de escritorio", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_Failure_HasNoDataField()
    {
        var json = JsonSerializer.Serialize(
            ToolResult.Failure<StockView>(ToolError.InvalidArguments("get_stock")),
            ToolJson.SerializerOptions);

        Assert.DoesNotContain("\"data\"", json, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"INVALID_ARGUMENTS\"", json, StringComparison.Ordinal);
    }
}
