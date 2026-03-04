namespace Delivery.Domain.Events;

/// <summary>
/// Domain event published to Service Bus topic delivery.completed when a driver
/// completes a delivery to the customer. Consumed by the Orders context to transition
/// order status to Delivered.
/// </summary>
public sealed record DeliveryCompleted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Unique delivery identifier.
    /// </summary>
    public required Guid DeliveryId { get; init; }

    /// <summary>
    /// Original order identifier from the Orders context.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// Identifier of the driver who completed the delivery.
    /// </summary>
    public required Guid DriverId { get; init; }

    /// <summary>
    /// Timestamp when delivery was completed.
    /// </summary>
    public required DateTimeOffset DeliveredAt { get; init; }

    /// <summary>
    /// Total minutes from OrderReadyForPickup (readyAt) to Delivered (deliveredAt).
    /// </summary>
    public required int TotalDeliveryMinutes { get; init; }

    /// <summary>
    /// True if totalDeliveryMinutes exceeds the 45-minute SLA threshold (DLV-R03).
    /// </summary>
    public required bool SlaBreached { get; init; }
}
