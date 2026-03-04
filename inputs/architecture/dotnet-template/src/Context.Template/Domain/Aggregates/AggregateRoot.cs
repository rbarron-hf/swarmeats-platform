using {ContextName}.Domain.Events;

namespace {ContextName}.Domain.Aggregates;

/// <summary>
/// Base class for all aggregate roots. Provides:
/// - Identity (<typeparamref name="TId"/>)
/// - Domain event collection (raised inside the aggregate, persisted via outbox)
/// - ETag for Cosmos DB optimistic concurrency
/// </summary>
/// <typeparam name="TId">Type of the aggregate identifier (typically Guid).</typeparam>
public abstract class AggregateRoot<TId>
{
    /// <summary>
    /// Unique identifier for this aggregate instance.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Cosmos DB ETag for optimistic concurrency control.
    /// Set by the repository on read; checked on write via IfMatchEtag.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Domain events raised during the current unit of work.
    /// Cleared by the repository after successful persistence.
    /// </summary>
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Read-only view of uncommitted domain events.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Raises a domain event. Call this inside aggregate methods
    /// after a state transition to record what happened.
    /// </summary>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all uncommitted domain events. Called by the repository
    /// after successful persistence to the outbox.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
