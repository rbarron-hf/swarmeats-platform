namespace Orders.Domain.Events;

/// <summary>
/// Domain event published to Service Bus topic orders.cancelled when a customer cancels an order.
/// Consumed by Restaurant and Delivery contexts.
/// </summary>
public sealed record OrderCancelled : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Unique order identifier.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// Human-readable order number (format: ORD-YYYYMMDD-sequential).
    /// </summary>
    public required string OrderNumber { get; init; }

    /// <summary>
    /// Restaurant identifier, used by downstream contexts to locate their local projection.
    /// </summary>
    public required Guid RestaurantId { get; init; }

    /// <summary>
    /// Timestamp of cancellation.
    /// </summary>
    public required DateTimeOffset CancelledAt { get; init; }
}
