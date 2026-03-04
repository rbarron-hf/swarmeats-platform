using Orders.Domain.ValueObjects;

namespace Orders.Domain.Exceptions;

/// <summary>
/// Thrown when an order cancellation is attempted but the order is not in Placed status.
/// Enforces business rule ORD-R05.
/// Maps to HTTP 409 with error code ORDER_CANNOT_CANCEL.
/// </summary>
public sealed class OrderCannotBeCancelledException : Exception
{
    public Guid OrderId { get; }
    public OrderStatus CurrentStatus { get; }
    public string ErrorCode => "ORDER_CANNOT_CANCEL";

    public OrderCannotBeCancelledException(Guid orderId, OrderStatus currentStatus)
        : base($"Order '{orderId}' cannot be cancelled. Current status is '{currentStatus}'. Cancellation is only permitted when the order status is 'Placed'.")
    {
        OrderId = orderId;
        CurrentStatus = currentStatus;
    }

    public OrderCannotBeCancelledException(Guid orderId, OrderStatus currentStatus, Exception innerException)
        : base($"Order '{orderId}' cannot be cancelled. Current status is '{currentStatus}'. Cancellation is only permitted when the order status is 'Placed'.", innerException)
    {
        OrderId = orderId;
        CurrentStatus = currentStatus;
    }
}
