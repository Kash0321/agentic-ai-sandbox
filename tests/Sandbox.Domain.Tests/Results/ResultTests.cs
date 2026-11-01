using Sandbox.Domain.Results;

namespace Sandbox.Domain.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_WithValue_ExposesValueAndNoError()
    {
        var result = Result.Success("valor");

        Assert.True(result.IsSuccess);
        Assert.Equal("valor", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_WithError_ExposesErrorAndNoValue()
    {
        var error = new DomainError("SOME_CODE", "Mensaje.", ErrorKind.Validation);

        var result = Result.Failure<string>(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.Value);
    }
}
