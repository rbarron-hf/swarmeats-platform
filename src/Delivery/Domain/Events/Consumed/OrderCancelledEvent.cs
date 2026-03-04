namespace Delivery.Domain.Events.Consumed;

/// <summary>
/// Represents the OrderCancelled event received from the Orders context
/// via Service Bus topic orders.cancelled. Used to cancel deliveries that
/// are still awaiting a driver.
/// </summary>
public sealed record OrderCancelledEvent
{
    /// <summary>
    /// The event type identifier (e.g., "OrderCancelled").
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
    public OrderCancelledPayload Payload { get; init; } = new();
}

/// <summary>
/// Payload of the OrderCancelled event containing order identification details.
/// </summary>
public sealed record OrderCancelledPayload
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid RestaurantId { get; init; }
    public DateTimeOffset CancelledAt { get; init; }
}
