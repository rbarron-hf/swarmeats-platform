namespace Restaurant.Domain.ValueObjects;

/// <summary>
/// Enumeration of all possible states in the RestaurantOrder aggregate lifecycle.
/// See FDD section 4.3 for the full state machine definition.
/// </summary>
public enum RestaurantOrderStatus
{
    Pending = 0,
    Accepted = 1,
    Preparing = 2,
    ReadyForPickup = 3,
    Rejected = 4,
    Cancelled = 5
}
