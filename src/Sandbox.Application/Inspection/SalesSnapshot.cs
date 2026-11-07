using Sandbox.Application.Sales;

namespace Sandbox.Application.Inspection;

/// <summary>
/// Foto del estado completo, sin filtrar por cliente: los pedidos ordenados por identificador y el stock ordenado por
/// producto. Sirve para evaluar escenarios, nunca para atender al cliente (spec <c>order-operations</c>, "Lectura de
/// inspección").
/// </summary>
public sealed record SalesSnapshot(IReadOnlyList<OrderView> Orders, IReadOnlyList<StockLevel> Stock);
