namespace Orders.Domain.ValueObjects;

/// <summary>
/// Enumeration of all possible states in the Order aggregate lifecycle.
/// See FDD section 3.2 for the full state machine definition.
/// </summary>
public enum OrderStatus
{
    Draft = 0,
    Placed = 1,
    Accepted = 2,
    Preparing = 3,
    ReadyForPickup = 4,
    InDelivery = 5,
    Delivered = 6,
    Rejected = 7,
    Cancelled = 8
}
