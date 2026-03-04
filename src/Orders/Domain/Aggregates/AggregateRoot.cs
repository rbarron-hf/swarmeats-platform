using Orders.Domain.Events;

namespace Orders.Domain.Aggregates;

/// <summary>
/// Base class for aggregate roots. Provides identity, domain event collection,
/// and concurrency control via an ETag.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier.</typeparam>
public abstract class AggregateRoot<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Unique identifier for the aggregate.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Cosmos DB ETag for optimistic concurrency control.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Returns a read-only view of uncommitted domain events.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to the uncommitted events collection.
    /// Called from within aggregate methods to record side effects.
    /// </summary>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all uncommitted domain events. Called after events have been dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
