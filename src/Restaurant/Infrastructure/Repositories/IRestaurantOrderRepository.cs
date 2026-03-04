using Restaurant.Domain.Aggregates;
using Restaurant.Domain.ValueObjects;

namespace Restaurant.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the RestaurantOrder aggregate.
/// Provides read and write access to restaurant orders stored in the Cosmos DB restaurant-orders container.
/// </summary>
public interface IRestaurantOrderRepository
{
    /// <summary>
    /// Loads a RestaurantOrder by its order identifier and restaurant identifier.
    /// Returns null if no order exists with the given IDs.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="restaurantId">The restaurant identifier (partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The RestaurantOrder aggregate, or null if not found.</returns>
    Task<RestaurantOrder?> GetByIdAsync(Guid orderId, Guid restaurantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all restaurant orders for a given restaurant, optionally filtered by status.
    /// </summary>
    /// <param name="restaurantId">The restaurant identifier (partition key).</param>
    /// <param name="status">Optional status filter. If null, returns all active orders.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of RestaurantOrder aggregates matching the criteria.</returns>
    Task<List<RestaurantOrder>> GetByRestaurantIdAsync(Guid restaurantId, RestaurantOrderStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the RestaurantOrder aggregate. Handles both insert and update via upsert.
    /// Also persists any uncommitted domain events to the outbox within the same
    /// Cosmos DB transactional batch for reliable event publishing.
    /// </summary>
    /// <param name="order">The RestaurantOrder aggregate to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(RestaurantOrder order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an order with the given source event ID has already been processed.
    /// Used for idempotent event handling.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="restaurantId">The restaurant identifier (partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the order already exists; false otherwise.</returns>
    Task<bool> ExistsAsync(Guid orderId, Guid restaurantId, CancellationToken cancellationToken = default);
}
