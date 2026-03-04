namespace Restaurant.Domain.Events;

/// <summary>
/// Domain event published to Service Bus topic restaurant.order-rejected when restaurant staff
/// reject an order or when an order is auto-rejected due to operating hours or unavailable items.
/// Consumed by the Orders context to transition the order status to Rejected.
/// Raised by RST-003 (auto-reject) and RST-005 (manual reject).
/// </summary>
public sealed record OrderRejected : IDomainEvent
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
    /// Rejection reason code (RESTAURANT_CLOSED, ITEM_UNAVAILABLE, TOO_BUSY, OTHER).
    /// </summary>
    public required string ReasonCode { get; init; }

    /// <summary>
    /// Menu item IDs that were unavailable. Empty if not applicable.
    /// </summary>
    public List<Guid> UnavailableItemIds { get; init; } = new();

    /// <summary>
    /// Timestamp when the order was rejected.
    /// </summary>
    public required DateTimeOffset RejectedAt { get; init; }
}
