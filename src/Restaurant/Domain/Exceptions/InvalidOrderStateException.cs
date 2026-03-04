using Restaurant.Domain.ValueObjects;

namespace Restaurant.Domain.Exceptions;

/// <summary>
/// Thrown when an invalid state transition is attempted on a RestaurantOrder.
/// Maps to HTTP 409. Error code depends on the context:
/// - RESTAURANT_ORDER_NOT_PENDING for accept/reject operations
/// - RESTAURANT_INVALID_TRANSITION for preparation status updates
/// </summary>
public sealed class InvalidOrderStateException : Exception
{
    public Guid OrderId { get; }
    public RestaurantOrderStatus CurrentStatus { get; }
    public RestaurantOrderStatus AttemptedStatus { get; }
    public string ErrorCode { get; }

    public InvalidOrderStateException(Guid orderId, RestaurantOrderStatus currentStatus, RestaurantOrderStatus attemptedStatus, string errorCode)
        : base($"Restaurant order '{orderId}' cannot transition from '{currentStatus}' to '{attemptedStatus}'.")
    {
        OrderId = orderId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
        ErrorCode = errorCode;
    }

    public InvalidOrderStateException(Guid orderId, RestaurantOrderStatus currentStatus, RestaurantOrderStatus attemptedStatus, string errorCode, Exception innerException)
        : base($"Restaurant order '{orderId}' cannot transition from '{currentStatus}' to '{attemptedStatus}'.", innerException)
    {
        OrderId = orderId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
        ErrorCode = errorCode;
    }
}
