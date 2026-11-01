using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>
/// Catálogo inicial por defecto, ficticio e igual en cada ejecución (design D5). Incluye un cliente bloqueado, un
/// producto sin stock y uno caro con el que 4 unidades caben en el importe máximo y 5 no.
/// </summary>
public static class SeedCatalog
{
    public static SalesState CreateState() =>
        new(
            [
                new Customer("CUST-001", "Ana Demo", CustomerStatus.Active),
                new Customer("CUST-002", "Bruno Demo", CustomerStatus.Active),
                new Customer("CUST-003", "Carla Demo", CustomerStatus.Blocked),
            ],
            [
                new StockItem("SKU-MUG-RED", "Taza roja", 12.50m, 20),
                new StockItem("SKU-TSHIRT-M", "Camiseta talla M", 19.90m, 5),
                new StockItem("SKU-LAMP-DESK", "Lámpara de escritorio", 89.00m, 0),
                new StockItem("SKU-CHAIR-ERGO", "Silla ergonómica", 249.00m, 10),
            ]);
}
