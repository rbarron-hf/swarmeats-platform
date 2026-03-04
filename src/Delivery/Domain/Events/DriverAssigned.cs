namespace Delivery.Domain.Events;

/// <summary>
/// Domain event published to Service Bus topic delivery.driver-assigned when a driver
/// is assigned to a delivery. Consumed by the Orders context to transition order status
/// to InDelivery.
/// </summary>
public sealed record DriverAssigned : IDomainEvent
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
    /// Identifier of the assigned driver.
    /// </summary>
    public required Guid DriverId { get; init; }

    /// <summary>
    /// Estimated minutes until delivery completion.
    /// </summary>
    public required int EstimatedArrivalMinutes { get; init; }

    /// <summary>
    /// Timestamp when the driver was assigned.
    /// </summary>
    public required DateTimeOffset AssignedAt { get; init; }
}
