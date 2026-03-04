namespace Restaurant.Domain.Exceptions;

/// <summary>
/// Thrown when a menu for the specified restaurant cannot be found.
/// Maps to HTTP 404 with error code RESTAURANT_NOT_FOUND.
/// </summary>
public sealed class MenuNotFoundException : Exception
{
    public Guid RestaurantId { get; }
    public string ErrorCode => "RESTAURANT_NOT_FOUND";

    public MenuNotFoundException(Guid restaurantId)
        : base($"Menu for restaurant with ID '{restaurantId}' was not found.")
    {
        RestaurantId = restaurantId;
    }

    public MenuNotFoundException(Guid restaurantId, Exception innerException)
        : base($"Menu for restaurant with ID '{restaurantId}' was not found.", innerException)
    {
        RestaurantId = restaurantId;
    }
}
