using System.Globalization;
using Sandbox.Domain.Results;
using Sandbox.Domain.Sales;

namespace Sandbox.Application.Sales;

/// <summary>
/// Commands sobre el estado in-memory. Cada uno hace todo su trabajo en una sola sección crítica: idempotencia,
/// validación, efectos y registro del resultado (design D5 y D6). La cancelación solo se comprueba al entrar, antes de
/// cualquier efecto.
/// </summary>
public sealed class SalesCommands(SalesState state, TimeProvider timeProvider) : ISalesCommands
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

        return Execute(command.IdempotencyKey, PlaceOrder, arguments, () => Place(command), cancellationToken);
    }

    public Task<Result<OrderView>> CancelOrderAsync(CancelOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Execute(
            command.IdempotencyKey,
            CancelOrder,
            [command.OrderId],
            () => Transition(command.OrderId, order => order.Cancel(state.Stock)),
            cancellationToken);
    }

    public Task<Result<OrderView>> ShipOrderAsync(ShipOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Execute(
            command.IdempotencyKey,
            ShipOrder,
            [command.OrderId],
            () => Transition(command.OrderId, order => order.Ship(state.Stock)),
            cancellationToken);
    }

    private Task<Result<OrderView>> Execute(
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
            if (state.Idempotency.Replay(idempotencyKey, operation, arguments) is { } replayed)
            {
                return replayed;
            }

            var outcome = run();
            if (outcome.IsSuccess)
            {
                state.Idempotency.Remember(idempotencyKey, operation, arguments, outcome.Value);
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

    private Result<OrderView> Transition(string orderId, Func<Order, Result<Order>> transition)
    {
        if (!state.Orders.TryGetValue(orderId, out var order))
        {
            return Result.Failure<OrderView>(SalesErrors.OrderNotFound());
        }

        var changed = transition(order);
        return changed.IsSuccess
            ? Result.Success(OrderView.From(changed.Value))
            : Result.Failure<OrderView>(changed.Error);
    }
}
