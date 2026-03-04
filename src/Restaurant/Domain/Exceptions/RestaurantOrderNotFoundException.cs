namespace Restaurant.Domain.Exceptions;

/// <summary>
/// Thrown when a RestaurantOrder with the specified identifier cannot be found.
/// Maps to HTTP 404 with error code RESTAURANT_ORDER_NOT_FOUND.
/// </summary>
public sealed class RestaurantOrderNotFoundException : Exception
{
    public Guid OrderId { get; }
    public Guid RestaurantId { get; }
    public string ErrorCode => "RESTAURANT_ORDER_NOT_FOUND";

    public RestaurantOrderNotFoundException(Guid orderId, Guid restaurantId)
        : base($"Restaurant order with order ID '{orderId}' for restaurant '{restaurantId}' was not found.")
    {
        OrderId = orderId;
        RestaurantId = restaurantId;
    }

    public RestaurantOrderNotFoundException(Guid orderId, Guid restaurantId, Exception innerException)
        : base($"Restaurant order with order ID '{orderId}' for restaurant '{restaurantId}' was not found.", innerException)
    {
        OrderId = orderId;
        RestaurantId = restaurantId;
    }
}
