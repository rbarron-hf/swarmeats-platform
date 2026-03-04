namespace {ContextName}.Domain.Events;

/// <summary>
/// Domain event raised when {description of what happened}.
/// Published to Service Bus topic: {topic.name}
///
/// Consumers:
///   - {ConsumerContext} ({StoryId}): {what consumer does with this event}
/// </summary>
public sealed class {EventName} : IDomainEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    // ── Event-specific payload fields ──

    /// <summary>Identifier of the affected aggregate.</summary>
    public Guid {AggregateId}Id { get; init; }

    // TODO: Add additional payload fields per the AC node data contract.
    // All fields should use init-only setters.
}
