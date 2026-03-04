namespace Restaurant.Domain.Events;

/// <summary>
/// Domain event published to Service Bus topic restaurant.order-accepted when restaurant staff accept an order.
/// Consumed by the Orders context to transition the order status to Accepted.
/// Raised by RST-004.
/// </summary>
public sealed record OrderAccepted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Original order identifier from the Orders context.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// Restaurant identifier.
    /// </summary>
    public required Guid RestaurantId { get; init; }

    /// <summary>
    /// Estimated preparation time in minutes (5-90 per RST-R03).
    /// </summary>
    public required int EstimatedPrepMinutes { get; init; }

    /// <summary>
    /// Timestamp when the order was accepted.
    /// </summary>
    public required DateTimeOffset AcceptedAt { get; init; }
}
