namespace Restaurant.Domain.Events;

/// <summary>
/// Marker interface for domain events raised by aggregates.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier for idempotent event processing.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
