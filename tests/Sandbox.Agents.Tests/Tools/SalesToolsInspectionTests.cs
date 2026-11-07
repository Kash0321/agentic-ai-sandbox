using Sandbox.Agents.Tools;
using Sandbox.Application.Inspection;

namespace Sandbox.Agents.Tests.Tools;

/// <summary>
/// La lectura de inspección no es una tool (spec <c>order-operations</c>, "Lectura de inspección", escenario "Fuera del
/// catálogo de tools"; design D4 de <c>add-scenario-engine</c>).
/// </summary>
public sealed class SalesToolsInspectionTests
{
    [Fact]
    public void Constructor_OfSalesTools_DoesNotReceiveTheInspector()
    {
        var parameterTypes = typeof(SalesTools).GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        Assert.DoesNotContain(typeof(SalesInspector), parameterTypes);
    }

    [Fact]
    public void All_Always_IsTheSixCustomerToolsWithoutAnInspectionTool()
    {
        Assert.Equal(
            ["list_products", "get_stock", "get_customer", "get_order", "place_order", "cancel_order"],
            SalesToolNames.All);
    }
}
