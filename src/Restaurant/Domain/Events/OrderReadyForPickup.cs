namespace Restaurant.Domain.Events;

/// <summary>
/// Domain event published to Service Bus topic restaurant.order-ready when restaurant staff
/// signal that the food is ready for driver pickup.
/// Consumed by the Delivery context to create a new Delivery aggregate.
/// Raised by RST-007.
/// </summary>
public sealed record OrderReadyForPickup : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Original order identifier from the Orders context.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// Restaurant identifier.
    /// </summary>
    public required Guid RestaurantId { get; init; }

    /// <summary>
    /// Restaurant address for driver pickup.
    /// </summary>
    public required RestaurantAddress RestaurantAddress { get; init; }

    /// <summary>
    /// Timestamp when the order was marked ready for pickup.
    /// </summary>
    public required DateTimeOffset ReadyAt { get; init; }
}

/// <summary>
/// Value object for the restaurant's physical location, included in the OrderReadyForPickup event.
/// </summary>
public sealed record RestaurantAddress(
    string Street,
    string City,
    string Postcode,
    decimal Latitude,
    decimal Longitude);
