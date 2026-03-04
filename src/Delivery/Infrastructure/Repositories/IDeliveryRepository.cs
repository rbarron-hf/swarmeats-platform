using Delivery.Domain.Aggregates;

namespace Delivery.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the Delivery aggregate.
/// Provides persistence operations for deliveries in Cosmos DB.
/// </summary>
public interface IDeliveryRepository
{
    /// <summary>
    /// Loads a Delivery aggregate by its unique identifier.
    /// Performs a cross-partition query since the caller may not know the partition key.
    /// Returns null if no delivery with the given ID exists.
    /// </summary>
    /// <param name="deliveryId">The unique delivery identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Delivery aggregate, or null if not found.</returns>
    Task<DeliveryAggregate?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a Delivery aggregate by orderId. Used by DLV-009 when handling OrderCancelled events.
    /// Performs a cross-partition query.
    /// Returns null if no delivery exists for the given order.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Delivery aggregate, or null if not found.</returns>
    Task<DeliveryAggregate?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the Delivery aggregate. Handles both insert and update via upsert.
    /// Also persists any uncommitted domain events to the outbox within the same
    /// Cosmos DB transactional batch for reliable event publishing.
    /// </summary>
    /// <param name="delivery">The Delivery aggregate to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(DeliveryAggregate delivery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Migrates a delivery document from the 'unassigned' sentinel partition to the
    /// driver's partition key. Required when a driver is assigned (DLV-002).
    /// Deletes the document from 'unassigned' and recreates it under the driver's partition.
    /// </summary>
    /// <param name="delivery">The Delivery aggregate with the newly assigned driver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MigratePartitionKeyAsync(DeliveryAggregate delivery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries for all deliveries in AwaitingDriver status that were created more than
    /// the specified number of minutes ago. Used by the UnassignedDeliveryMonitor (DLV-008).
    /// </summary>
    /// <param name="olderThanMinutes">Minimum age in minutes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of overdue unassigned deliveries.</returns>
    Task<List<DeliveryAggregate>> GetOverdueUnassignedDeliveriesAsync(int olderThanMinutes, CancellationToken cancellationToken = default);
}
