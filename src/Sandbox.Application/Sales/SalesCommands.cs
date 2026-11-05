using System.Globalization;
using Sandbox.Application.Auditing;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>
/// Commands sobre el estado in-memory. Cada uno hace todo su trabajo en una sola sección crítica: idempotencia,
/// autorización, validación, efectos y registro del resultado (design D5 y D6 de <c>add-order-domain</c>, D1 y D3 de
/// <c>add-agent-guardrails</c>). La cancelación solo se comprueba al entrar, antes de cualquier efecto.
/// </summary>
public sealed class SalesCommands(SalesState state, TimeProvider timeProvider, AuditLog audit) : ISalesCommands
{
    private const string PlaceOrder = nameof(PlaceOrder);
    private const string CancelOrder = nameof(CancelOrder);
    private const string ShipOrder = nameof(ShipOrder);

    public Task<Result<OrderView>> PlaceOrderAsync(PlaceOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        IReadOnlyList<string> arguments =
        [
            command.CustomerId,
            .. command.Lines.SelectMany(line => new[] { line.Sku, line.Quantity.ToString(CultureInfo.InvariantCulture) }),
        ];

        return Execute(
            IdempotencyScope.ForCustomer(command.CustomerId),
            command.IdempotencyKey,
            PlaceOrder,
            arguments,
            () => Place(command),
            cancellationToken);
    }

    public Task<Result<OrderView>> CancelOrderAsync(CancelOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Execute(
            IdempotencyScope.ForCustomer(command.CustomerId),
            command.IdempotencyKey,
            CancelOrder,
            [command.OrderId],
            () => Transition(
                OrderAccess.FindOwned(state, command.OrderId, command.CustomerId, CancelOrder, audit),
                order => order.Cancel(state.Stock)),
            cancellationToken);
    }

    // Enviar es una operación del almacén, sin cliente: no hay comprobación de pertenencia.
    public Task<Result<OrderView>> ShipOrderAsync(ShipOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Execute(
            IdempotencyScope.Warehouse,
            command.IdempotencyKey,
            ShipOrder,
            [command.OrderId],
            () => Transition(Find(command.OrderId), order => order.Ship(state.Stock)),
            cancellationToken);
    }

    private Task<Result<OrderView>> Execute(
        IdempotencyScope scope,
        string idempotencyKey,
        string operation,
        IReadOnlyList<string> arguments,
        Func<Result<OrderView>> run,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IdempotencyKey.IsValid(idempotencyKey))
        {
            return Task.FromResult(Result.Failure<OrderView>(OperationErrors.InvalidIdempotencyKey()));
        }

        var result = state.Locked(() =>
        {
            if (state.Idempotency.Replay(scope, idempotencyKey, operation, arguments) is { } replayed)
            {
                return replayed;
            }

            var outcome = run();
            if (outcome.IsSuccess)
            {
                state.Idempotency.Remember(scope, idempotencyKey, operation, arguments, outcome.Value);
            }

            return outcome;
        });

        return Task.FromResult(result);
    }

    // Orden de evaluación de la spec: las líneas antes que la existencia del cliente; el resto lo decide Order.Create.
    private Result<OrderView> Place(PlaceOrderCommand command)
    {
        var lines = OrderPolicy.ValidateLines(command.Lines);
        if (!lines.IsSuccess)
        {
            return Result.Failure<OrderView>(lines.Error);
        }

        if (!state.Customers.TryGetValue(command.CustomerId, out var customer))
        {
            return Result.Failure<OrderView>(SalesErrors.CustomerNotFound());
        }

        var created = Order.Create(state.NextOrderId, customer, command.Lines, state.Stock, timeProvider.GetUtcNow());
        if (!created.IsSuccess)
        {
            return Result.Failure<OrderView>(created.Error);
        }

        state.AddOrder(created.Value);
        return Result.Success(OrderView.From(created.Value));
    }

    private Result<Order> Find(string orderId) =>
        state.Orders.TryGetValue(orderId, out var order)
            ? Result.Success(order)
            : Result.Failure<Order>(SalesErrors.OrderNotFound());

    private static Result<OrderView> Transition(Result<Order> found, Func<Order, Result<Order>> transition)
    {
        if (!found.IsSuccess)
        {
            return Result.Failure<OrderView>(found.Error);
        }

        var changed = transition(found.Value);
        return changed.IsSuccess
            ? Result.Success(OrderView.From(changed.Value))
            : Result.Failure<OrderView>(changed.Error);
    }
}
