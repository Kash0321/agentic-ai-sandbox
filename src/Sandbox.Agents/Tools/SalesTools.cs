using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Agents.Tools;

/// <summary>
/// Tools del agente de ventas. Son finas: traducen argumentos a una operación de aplicación y su resultado a
/// <see cref="ToolResult{T}"/>, sin reglas de negocio. Cada una captura lo inesperado de su propio cuerpo (borde
/// interior, design D3); la cancelación se deja pasar.
/// </summary>
public sealed partial class SalesTools(
    ISalesCommands commands,
    ISalesQueries queries,
    SalesSession session,
    ILogger<SalesTools> logger)
{
    private const string IdempotencyKeyDescription =
        "Clave de idempotencia: genera una nueva para cada petición distinta y, si repites la misma petición tras un " +
        "error reintentable, usa exactamente la misma clave. De 1 a 64 caracteres.";

    /// <summary>Las seis tools con sus nombres estables, listas para enviarlas al modelo.</summary>
    public IReadOnlyList<AIFunction> CreateFunctions() =>
    [
        Create(ListProductsAsync, SalesToolNames.ListProducts),
        Create(GetStockAsync, SalesToolNames.GetStock),
        Create(GetCustomerAsync, SalesToolNames.GetCustomer),
        Create(GetOrderAsync, SalesToolNames.GetOrder),
        Create(PlaceOrderAsync, SalesToolNames.PlaceOrder),
        Create(CancelOrderAsync, SalesToolNames.CancelOrder),
    ];

    [Description(
        "Lista todos los productos del catálogo con su SKU, nombre, precio unitario en EUR y unidades disponibles. " +
        "Úsala para encontrar el SKU de lo que pide el cliente.")]
    public Task<ToolResult<IReadOnlyList<StockView>>> ListProductsAsync(CancellationToken cancellationToken) =>
        RunAsync(SalesToolNames.ListProducts, () => queries.ListProductsAsync(new ListProductsQuery(), cancellationToken));

    [Description("Devuelve el nombre, el precio unitario en EUR y las unidades disponibles de un producto.")]
    public Task<ToolResult<StockView>> GetStockAsync(
        [Description("SKU del producto, tal como lo devuelve list_products (por ejemplo, SKU-MUG-RED).")] string sku,
        CancellationToken cancellationToken) =>
        RunAsync(SalesToolNames.GetStock, () => queries.GetStockAsync(new GetStockQuery(sku), cancellationToken));

    [Description("Devuelve el identificador, el nombre y el estado del cliente con el que hablas.")]
    public Task<ToolResult<CustomerView>> GetCustomerAsync(CancellationToken cancellationToken) =>
        RunAsync(
            SalesToolNames.GetCustomer,
            () => queries.GetCustomerAsync(new GetCustomerQuery(session.CustomerId), cancellationToken));

    [Description("Devuelve un pedido: su estado, sus líneas, el importe total en EUR y la fecha de creación.")]
    public Task<ToolResult<OrderView>> GetOrderAsync(
        [Description("Identificador del pedido, con el formato ORD-0001.")] string orderId,
        CancellationToken cancellationToken) =>
        RunAsync(SalesToolNames.GetOrder, () => queries.GetOrderAsync(new GetOrderQuery(orderId), cancellationToken));

    [Description(
        "Crea un pedido para el cliente con el que hablas y reserva su stock. Devuelve el pedido creado, o un error " +
        "con un código y un mensaje que explica cómo corregir la petición.")]
    public Task<ToolResult<OrderView>> PlaceOrderAsync(
        [Description("Líneas del pedido: un producto por línea, con su SKU y sus unidades.")] IReadOnlyList<OrderLineArgument> lines,
        [Description(IdempotencyKeyDescription)] string idempotencyKey,
        CancellationToken cancellationToken) =>
        RunAsync(
            SalesToolNames.PlaceOrder,
            () => commands.PlaceOrderAsync(
                new PlaceOrderCommand(
                    idempotencyKey,
                    session.CustomerId,
                    // Un modelo puede enviar null en lugar de una lista; el dominio lo trata como pedido vacío.
                    [.. (lines ?? []).Select(line => new OrderLineRequest(line.Sku, line.Quantity))]),
                cancellationToken));

    [Description("Cancela un pedido en curso del cliente con el que hablas y libera su stock.")]
    public Task<ToolResult<OrderView>> CancelOrderAsync(
        [Description("Identificador del pedido, con el formato ORD-0001.")] string orderId,
        [Description(IdempotencyKeyDescription)] string idempotencyKey,
        CancellationToken cancellationToken) =>
        RunAsync(
            SalesToolNames.CancelOrder,
            () => commands.CancelOrderAsync(new CancelOrderCommand(idempotencyKey, orderId), cancellationToken));

    private static AIFunction Create(Delegate method, string name) =>
        AIFunctionFactory.Create(
            method,
            new AIFunctionFactoryOptions { Name = name, SerializerOptions = ToolJson.SerializerOptions });

    private async Task<ToolResult<T>> RunAsync<T>(string toolName, Func<Task<Result<T>>> operation)
    {
        try
        {
            return ToolResult.From(await operation().ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUnexpectedFailure(logger, toolName, exception);
            return ToolResult.Failure<T>(ToolError.Internal());
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo inesperado en la tool {Tool}")]
    private static partial void LogUnexpectedFailure(ILogger logger, string tool, Exception exception);
}
