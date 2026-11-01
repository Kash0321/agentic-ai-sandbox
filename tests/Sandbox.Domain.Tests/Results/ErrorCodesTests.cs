using System.Reflection;
using System.Text.RegularExpressions;
using Sandbox.Domain.Results;

namespace Sandbox.Domain.Tests.Results;

public sealed partial class ErrorCodesTests
{
    [Fact]
    public void Codes_InCatalog_AreUnique()
    {
        var codes = AllCodes();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Codes_InCatalog_AreUpperCaseWithUnderscores()
    {
        Assert.All(AllCodes(), code => Assert.Matches(UpperSnakeCase(), code));
    }

    private static List<string> AllCodes() =>
        [.. typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)]; // Todas las constantes del catálogo son cadenas.

    [GeneratedRegex("^[A-Z]+(_[A-Z]+)*$")]
    private static partial Regex UpperSnakeCase();
}
