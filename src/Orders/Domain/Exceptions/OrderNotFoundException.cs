namespace Orders.Domain.Exceptions;

/// <summary>
/// Thrown when an order with the specified identifier cannot be found.
/// Maps to HTTP 404 with error code ORDER_NOT_FOUND.
/// </summary>
public sealed class OrderNotFoundException : Exception
{
    public Guid OrderId { get; }
    public string ErrorCode => "ORDER_NOT_FOUND";

    public OrderNotFoundException(Guid orderId)
        : base($"Order with ID '{orderId}' was not found.")
    {
        OrderId = orderId;
    }

    public OrderNotFoundException(Guid orderId, Exception innerException)
        : base($"Order with ID '{orderId}' was not found.", innerException)
    {
        OrderId = orderId;
    }
}
