using System.Globalization;

namespace Sandbox.Scenarios.Tests;

public sealed class GlobalizationTests
{
    [Fact]
    public void ToString_WithCurrentCulture_UsesInvariantFormat()
    {
        var formatted = 1234.5.ToString("N2", CultureInfo.CurrentCulture);

        Assert.Equal("1,234.50", formatted);
    }

    [Fact]
    public void GetCultureInfo_WithSpecificCulture_ThrowsCultureNotFound()
    {
        Assert.Throws<CultureNotFoundException>(() => CultureInfo.GetCultureInfo("es-ES"));
    }
}
