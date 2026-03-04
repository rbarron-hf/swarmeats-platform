using Orders.Domain.ValueObjects;

namespace Orders.Domain.Exceptions;

/// <summary>
/// Thrown when an invalid state transition is attempted on an Order aggregate.
/// For Service Bus event handlers, this exception is caught, logged as a warning,
/// and the event is discarded without rethrowing to prevent poison message scenarios.
/// </summary>
public sealed class InvalidOrderTransitionException : Exception
{
    /// <summary>
    /// The order identifier.
    /// </summary>
    public Guid OrderId { get; }

    /// <summary>
    /// The current status of the order.
    /// </summary>
    public OrderStatus CurrentStatus { get; }

    /// <summary>
    /// The target status that was attempted.
    /// </summary>
    public OrderStatus TargetStatus { get; }

    /// <summary>
    /// Machine-readable error code.
    /// </summary>
    public string ErrorCode => "ORDER_INVALID_TRANSITION";

    public InvalidOrderTransitionException(Guid orderId, OrderStatus currentStatus, OrderStatus targetStatus)
        : base($"Order '{orderId}' cannot transition from '{currentStatus}' to '{targetStatus}'.")
    {
        OrderId = orderId;
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }

    public InvalidOrderTransitionException(Guid orderId, OrderStatus currentStatus, OrderStatus targetStatus, Exception innerException)
        : base($"Order '{orderId}' cannot transition from '{currentStatus}' to '{targetStatus}'.", innerException)
    {
        OrderId = orderId;
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }
}
