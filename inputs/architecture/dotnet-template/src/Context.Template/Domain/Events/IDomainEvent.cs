namespace {ContextName}.Domain.Events;

/// <summary>
/// Marker interface for all domain events raised by aggregates.
/// Every event carries a unique EventId and the timestamp it occurred.
/// Events are persisted to the outbox and published to Service Bus.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier for this specific event instance.
    /// Used for idempotency checks by consumers.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// UTC timestamp when the event was raised inside the aggregate.
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
