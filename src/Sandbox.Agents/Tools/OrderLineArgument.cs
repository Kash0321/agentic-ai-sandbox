using System.ComponentModel;

namespace Sandbox.Agents.Tools;

/// <summary>Línea de pedido tal como la envía el modelo. Las descripciones van al esquema de <c>place_order</c>.</summary>
public sealed record OrderLineArgument(
    [property: Description("SKU del producto, tal como lo devuelve list_products (por ejemplo, SKU-MUG-RED).")] string Sku,
    [property: Description("Unidades que pide el cliente de ese producto.")] int Quantity);
