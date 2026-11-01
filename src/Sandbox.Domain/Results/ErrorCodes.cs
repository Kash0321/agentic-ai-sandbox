namespace Sandbox.Domain.Results;

/// <summary>Catálogo de códigos de error estables (specs <c>order-domain</c>, <c>order-operations</c> y <c>fault-injection</c>).</summary>
public static class ErrorCodes
{
    public const string EmptyOrder = "EMPTY_ORDER";

    public const string InvalidQuantity = "INVALID_QUANTITY";

    public const string DuplicateOrderLine = "DUPLICATE_ORDER_LINE";

    public const string CustomerBlocked = "CUSTOMER_BLOCKED";

    public const string OrderLimitExceeded = "ORDER_LIMIT_EXCEEDED";

    public const string InsufficientStock = "INSUFFICIENT_STOCK";

    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";

    public const string CustomerNotFound = "CUSTOMER_NOT_FOUND";

    public const string ProductNotFound = "PRODUCT_NOT_FOUND";

    public const string OrderNotFound = "ORDER_NOT_FOUND";

    public const string IdempotencyKeyConflict = "IDEMPOTENCY_KEY_CONFLICT";

    public const string InvalidIdempotencyKey = "INVALID_IDEMPOTENCY_KEY";

    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
}
