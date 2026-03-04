using {ContextName}.Domain.Aggregates;

namespace {ContextName}.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the {Aggregate} aggregate.
///
/// Defined in Infrastructure (not Domain) because the handler references it,
/// but the domain layer itself has zero infrastructure dependencies.
///
/// Implementations must:
///   - Use Cosmos DB transactional batch for atomicity
///   - Persist domain events to the outbox in the same transaction
///   - Set ETag on read for optimistic concurrency
///   - Check IfMatchEtag on write
/// </summary>
public interface I{Aggregate}Repository
{
    /// <summary>
    /// Loads an aggregate by its unique identifier.
    /// Returns null if no document with the given ID exists.
    /// </summary>
    Task<{AggregateType}?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the aggregate. Handles both insert (new) and update (existing)
    /// via Cosmos DB upsert. Also persists any uncommitted domain events to
    /// the outbox within the same transactional batch.
    /// </summary>
    Task SaveAsync({AggregateType} aggregate, CancellationToken cancellationToken = default);
}
