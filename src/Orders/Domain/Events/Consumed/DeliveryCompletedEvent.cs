namespace Orders.Domain.Events.Consumed;

/// <summary>
/// Event DTO for the DeliveryCompleted event consumed from the Delivery context
/// via Service Bus topic delivery.completed.
/// Matches the event envelope contract defined in FDD section 6.2.
/// This is the terminal happy-path event for an order.
/// </summary>
public sealed record DeliveryCompletedEvent
{
    /// <summary>
    /// Event type identifier (always "DeliveryCompleted").
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
    /// Source bounded context (always "Delivery").
    /// </summary>
    public string SourceContext { get; init; } = string.Empty;

    /// <summary>
    /// Event-specific payload.
    /// </summary>
    public DeliveryCompletedPayload Payload { get; init; } = new();
}

/// <summary>
/// Payload data for the DeliveryCompleted event.
/// </summary>
public sealed record DeliveryCompletedPayload
{
    /// <summary>
    /// The delivery identifier from the Delivery context.
    /// </summary>
    public Guid DeliveryId { get; init; }

    /// <summary>
    /// The order identifier from the Orders context.
    /// </summary>
    public Guid OrderId { get; init; }

    /// <summary>
    /// The identifier of the driver who completed the delivery.
    /// </summary>
    public Guid DriverId { get; init; }

    /// <summary>
    /// Timestamp when the delivery was completed.
    /// </summary>
    public DateTimeOffset DeliveredAt { get; init; }

    /// <summary>
    /// Total minutes from OrderReadyForPickup to Delivered.
    /// </summary>
    public int TotalDeliveryMinutes { get; init; }

    /// <summary>
    /// Whether the 45-minute SLA was breached.
    /// </summary>
    public bool SlaBreached { get; init; }
}
