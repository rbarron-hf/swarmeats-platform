namespace Delivery.Domain.Events.Consumed;

/// <summary>
/// Represents the OrderReadyForPickup event received from the Restaurant context
/// via Service Bus topic restaurant.order-ready. Used to create new Delivery aggregates.
/// </summary>
public sealed record OrderReadyForPickupEvent
{
    /// <summary>
    /// The event type identifier (e.g., "OrderReadyForPickup").
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
    /// The bounded context that published this event.
    /// </summary>
    public string SourceContext { get; init; } = string.Empty;

    /// <summary>
    /// Event-specific payload.
    /// </summary>
    public OrderReadyForPickupPayload Payload { get; init; } = new();
}

/// <summary>
/// Payload of the OrderReadyForPickup event containing order and address details.
/// </summary>
public sealed record OrderReadyForPickupPayload
{
    public Guid OrderId { get; init; }
    public Guid RestaurantId { get; init; }
    public EventAddress RestaurantAddress { get; init; } = new();
    public EventAddress DeliveryAddress { get; init; } = new();
    public DateTimeOffset ReadyAt { get; init; }
}

/// <summary>
/// Address value within an event payload.
/// </summary>
public sealed record EventAddress
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
