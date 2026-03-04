namespace Restaurant.Domain.Events.Consumed;

/// <summary>
/// Represents the OrderCancelled event received from the Orders context via Service Bus topic orders.cancelled.
/// This is the external event contract consumed by RST-008 (HandleOrderCancelledFunction).
/// </summary>
public sealed record OrderCancelledEvent
{
    /// <summary>
    /// The Service Bus message envelope event type.
    /// </summary>
    public string EventType { get; init; } = "OrderCancelled";

    /// <summary>
    /// Unique event identifier for idempotent processing.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Source bounded context name.
    /// </summary>
    public string SourceContext { get; init; } = "Orders";

    /// <summary>
    /// Event payload containing cancellation details.
    /// </summary>
    public required OrderCancelledPayload Payload { get; init; }
}

/// <summary>
/// Payload of the OrderCancelled event.
/// </summary>
public sealed record OrderCancelledPayload
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid RestaurantId { get; init; }
    public DateTimeOffset CancelledAt { get; init; }
}
