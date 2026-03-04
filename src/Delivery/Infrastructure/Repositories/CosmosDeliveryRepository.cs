using System.Net;
using Microsoft.Azure.Cosmos;
using Delivery.Domain.Aggregates;
using Delivery.Domain.ValueObjects;
using Delivery.Infrastructure.Models;

namespace Delivery.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation of IDeliveryRepository.
/// Uses the deliveries container with driverId as partition key.
/// Deliveries in AwaitingDriver status use "unassigned" as the sentinel partition key.
/// Persists domain events to the outbox within the same transactional batch.
/// </summary>
public sealed class CosmosDeliveryRepository : IDeliveryRepository
{
    private readonly Container _deliveriesContainer;
    private readonly Container _outboxContainer;

    /// <summary>
    /// Sentinel partition key value used for deliveries that have no driver assigned yet.
    /// </summary>
    public const string UnassignedPartitionKey = "unassigned";

    public CosmosDeliveryRepository(CosmosClient cosmosClient, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var database = cosmosClient.GetDatabase(databaseName);
        _deliveriesContainer = database.GetContainer("deliveries");
        _outboxContainer = database.GetContainer("deliveries-outbox");
    }

    /// <inheritdoc />
    public async Task<DeliveryAggregate?> GetByIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @deliveryId")
                .WithParameter("@deliveryId", deliveryId.ToString());

            var iterator = _deliveriesContainer.GetItemQueryIterator<DeliveryAggregate>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var delivery = response.FirstOrDefault();
                if (delivery is not null)
                {
                    delivery.ETag = response.ETag;
                    return delivery;
                }
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<DeliveryAggregate?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.orderId = @orderId")
                .WithParameter("@orderId", orderId.ToString());

            var iterator = _deliveriesContainer.GetItemQueryIterator<DeliveryAggregate>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var delivery = response.FirstOrDefault();
                if (delivery is not null)
                {
                    delivery.ETag = response.ETag;
                    return delivery;
                }
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(DeliveryAggregate delivery, CancellationToken cancellationToken = default)
    {
        var partitionKeyValue = delivery.DriverId?.ToString() ?? UnassignedPartitionKey;
        var partitionKey = new PartitionKey(partitionKeyValue);

        // Use a transactional batch to atomically persist the delivery and any outbox messages.
        var batch = _deliveriesContainer.CreateTransactionalBatch(partitionKey);

        // Upsert the delivery document
        batch.UpsertItem(delivery, new TransactionalBatchItemRequestOptions
        {
            IfMatchEtag = delivery.ETag
        });

        // Persist each domain event as an outbox message in the same partition
        foreach (var domainEvent in delivery.DomainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = delivery.Id.ToString(),
                EventType = domainEvent.GetType().Name,
                Payload = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                CreatedAt = DateTimeOffset.UtcNow,
                Processed = false
            };

            batch.CreateItem(outboxMessage);
        }

        var batchResponse = await batch.ExecuteAsync(cancellationToken);

        if (!batchResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to save delivery '{delivery.Id}'. Cosmos DB transactional batch returned status code {batchResponse.StatusCode}.");
        }

        // Clear domain events after successful persistence
        delivery.ClearDomainEvents();
    }

    /// <inheritdoc />
    public async Task MigratePartitionKeyAsync(DeliveryAggregate delivery, CancellationToken cancellationToken = default)
    {
        // Step 1: Delete the document from the 'unassigned' partition
        try
        {
            await _deliveriesContainer.DeleteItemAsync<DeliveryAggregate>(
                delivery.Id.ToString(),
                new PartitionKey(UnassignedPartitionKey),
                cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Document may have already been migrated (idempotent handling)
        }

        // Step 2: Save the document under the new driver partition key
        await SaveAsync(delivery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<DeliveryAggregate>> GetOverdueUnassignedDeliveriesAsync(
        int olderThanMinutes,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-olderThanMinutes);

        var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.status = @status AND c.createdAt < @cutoff")
            .WithParameter("@status", (int)DeliveryStatus.AwaitingDriver)
            .WithParameter("@cutoff", cutoff.ToString("o"));

        var results = new List<DeliveryAggregate>();
        var iterator = _deliveriesContainer.GetItemQueryIterator<DeliveryAggregate>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(UnassignedPartitionKey)
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }
}
