namespace Orders.Domain.Events.Consumed;

/// <summary>
/// Event DTO for the OrderReadyForPickup event consumed from the Restaurant context
/// via Service Bus topic restaurant.order-ready.
/// Matches the event envelope contract defined in FDD section 6.2.
/// </summary>
public sealed record OrderReadyForPickupEvent
{
    /// <summary>
    /// Event type identifier (always "OrderReadyForPickup").
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Unique event identifier for idempotent processing.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Source bounded context (always "Restaurant").
    /// </summary>
    public string SourceContext { get; init; } = string.Empty;

    /// <summary>
    /// Event-specific payload.
    /// </summary>
    public OrderReadyForPickupPayload Payload { get; init; } = new();
}

/// <summary>
/// Payload data for the OrderReadyForPickup event.
/// </summary>
public sealed record OrderReadyForPickupPayload
{
    /// <summary>
    /// The order identifier from the Orders context.
    /// </summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// The restaurant identifier.
    /// </summary>
    public Guid RestaurantId { get; init; }

    /// <summary>
    /// The restaurant's physical address for driver pickup.
    /// </summary>
    public RestaurantAddressPayload RestaurantAddress { get; init; } = new();

    /// <summary>
    /// Timestamp when the food was ready for pickup.
    /// </summary>
    public DateTimeOffset ReadyAt { get; init; }
}

/// <summary>
/// Restaurant address data within the OrderReadyForPickup event payload.
/// </summary>
public sealed record RestaurantAddressPayload
{
    /// <summary>
    /// Street address.
    /// </summary>
    public string Street { get; init; } = string.Empty;

    /// <summary>
    /// City name.
    /// </summary>
    public string City { get; init; } = string.Empty;

    /// <summary>
    /// Postal code.
    /// </summary>
    public string Postcode { get; init; } = string.Empty;

    /// <summary>
    /// Latitude coordinate.
    /// </summary>
    public double Latitude { get; init; }

    /// <summary>
    /// Longitude coordinate.
    /// </summary>
    public double Longitude { get; init; }
}
