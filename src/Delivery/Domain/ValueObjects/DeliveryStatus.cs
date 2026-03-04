namespace Delivery.Domain.ValueObjects;

/// <summary>
/// Enumeration of all possible states in the Delivery aggregate lifecycle.
/// See FDD section 5.2 for the full state machine definition.
/// AwaitingDriver -> DriverAssigned -> PickedUp -> Delivered
/// Cancellation is possible at any pre-Delivered state.
/// </summary>
public enum DeliveryStatus
{
    AwaitingDriver = 0,
    DriverAssigned = 1,
    PickedUp = 2,
    Delivered = 3,
    Cancelled = 4
}
