namespace Restaurant.Domain.Exceptions;

/// <summary>
/// Thrown when an order is received outside the restaurant's operating hours.
/// Used in RST-003 for auto-rejection per business rule RST-R01.
/// </summary>
public sealed class RestaurantClosedException : Exception
{
    public Guid RestaurantId { get; }
    public string ErrorCode => "RESTAURANT_CLOSED";

    public RestaurantClosedException(Guid restaurantId)
        : base($"Restaurant '{restaurantId}' is outside operating hours.")
    {
        RestaurantId = restaurantId;
    }

    public RestaurantClosedException(Guid restaurantId, Exception innerException)
        : base($"Restaurant '{restaurantId}' is outside operating hours.", innerException)
    {
        RestaurantId = restaurantId;
    }
}
