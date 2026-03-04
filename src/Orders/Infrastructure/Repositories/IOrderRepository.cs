using Orders.Domain.Aggregates;

namespace Orders.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the Order aggregate.
/// Defined in Infrastructure (not Domain) because the handler references it,
/// but the domain layer itself has zero infrastructure dependencies.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Loads an Order aggregate by its unique identifier.
    /// Returns null if no order with the given ID exists.
    /// </summary>
    /// <param name="orderId">The unique order identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Order aggregate, or null if not found.</returns>
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads all Order aggregates for a given customer, sorted by creation date descending.
    /// Supports pagination via Cosmos DB continuation tokens.
    /// </summary>
    /// <param name="customerId">The customer identifier (Cosmos DB partition key).</param>
    /// <param name="continuationToken">Optional continuation token for pagination.</param>
    /// <param name="pageSize">Maximum number of orders to return per page. Default is 20.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the list of orders and an optional continuation token for the next page.</returns>
    Task<(List<Order> Orders, string? ContinuationToken)> GetByCustomerIdAsync(
        Guid customerId,
        string? continuationToken = null,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the Order aggregate. Handles both insert and update via upsert.
    /// Also persists any uncommitted domain events to the outbox within the same
    /// Cosmos DB transactional batch for reliable event publishing.
    /// </summary>
    /// <param name="order">The Order aggregate to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(Order order, CancellationToken cancellationToken = default);
}
