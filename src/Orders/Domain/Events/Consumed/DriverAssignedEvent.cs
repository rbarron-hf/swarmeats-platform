namespace Orders.Domain.Events.Consumed;

/// <summary>
/// Event DTO for the DriverAssigned event consumed from the Delivery context
/// via Service Bus topic delivery.driver-assigned.
/// Matches the event envelope contract defined in FDD section 6.2.
/// </summary>
public sealed record DriverAssignedEvent
{
    /// <summary>
    /// Event type identifier (always "DriverAssigned").
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
    public DriverAssignedPayload Payload { get; init; } = new();
}

/// <summary>
/// Payload data for the DriverAssigned event.
/// </summary>
public sealed record DriverAssignedPayload
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
    /// The identifier of the assigned driver.
    /// </summary>
    public Guid DriverId { get; init; }

    /// <summary>
    /// Estimated minutes until delivery completion.
    /// </summary>
    public int EstimatedArrivalMinutes { get; init; }

    /// <summary>
    /// Timestamp when the driver was assigned.
    /// </summary>
    public DateTimeOffset AssignedAt { get; init; }
}
