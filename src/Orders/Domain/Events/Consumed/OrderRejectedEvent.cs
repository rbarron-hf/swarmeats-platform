namespace Orders.Domain.Events.Consumed;

/// <summary>
/// Event DTO for the OrderRejected event consumed from the Restaurant context
/// via Service Bus topic restaurant.order-rejected.
/// Matches the event envelope contract defined in FDD section 6.2.
/// </summary>
public sealed record OrderRejectedEvent
{
    /// <summary>
    /// Event type identifier (always "OrderRejected").
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
    public OrderRejectedPayload Payload { get; init; } = new();
}

/// <summary>
/// Payload data for the OrderRejected event.
/// </summary>
public sealed record OrderRejectedPayload
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
    /// Rejection reason code (e.g., RESTAURANT_CLOSED, ITEM_UNAVAILABLE, TOO_BUSY, OTHER).
    /// </summary>
    public string ReasonCode { get; init; } = string.Empty;

    /// <summary>
    /// Menu item IDs that were unavailable (empty if not applicable).
    /// </summary>
    public List<Guid> UnavailableItemIds { get; init; } = new();

    /// <summary>
    /// Timestamp when the restaurant rejected the order.
    /// </summary>
    public DateTimeOffset RejectedAt { get; init; }
}
