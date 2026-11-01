using Sandbox.Domain.Results;

namespace Sandbox.Application.Tests.Sales;

internal static class ResultAssert
{
    public static DomainError Fails<T>(Result<T> result, string expectedCode)
    {
        Assert.False(result.IsSuccess, $"Se esperaba {expectedCode} y la operación tuvo éxito.");
        Assert.Equal(expectedCode, result.Error.Code);
        return result.Error;
    }
}
