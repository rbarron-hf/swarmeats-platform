namespace Orders.Domain.Events.Consumed;

/// <summary>
/// Event DTO for the OrderAccepted event consumed from the Restaurant context
/// via Service Bus topic restaurant.order-accepted.
/// Matches the event envelope contract defined in FDD section 6.2.
/// </summary>
public sealed record OrderAcceptedEvent
{
    /// <summary>
    /// Event type identifier (always "OrderAccepted").
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
    public OrderAcceptedPayload Payload { get; init; } = new();
}

/// <summary>
/// Payload data for the OrderAccepted event.
/// </summary>
public sealed record OrderAcceptedPayload
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
    /// Estimated preparation time in minutes provided by restaurant staff.
    /// </summary>
    public int EstimatedPrepMinutes { get; init; }

    /// <summary>
    /// Timestamp when the restaurant accepted the order.
    /// </summary>
    public DateTimeOffset AcceptedAt { get; init; }
}
