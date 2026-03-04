using System.Net;
using Microsoft.Azure.Cosmos;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.ValueObjects;
using Restaurant.Infrastructure.Models;

namespace Restaurant.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation of IRestaurantOrderRepository.
/// Uses the restaurant-orders container with restaurantId as partition key.
/// Persists domain events to the outbox within the same transactional batch.
/// </summary>
public sealed class CosmosRestaurantOrderRepository : IRestaurantOrderRepository
{
    private readonly Container _ordersContainer;
    private readonly Container _outboxContainer;

    public CosmosRestaurantOrderRepository(CosmosClient cosmosClient, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var database = cosmosClient.GetDatabase(databaseName);
        _ordersContainer = database.GetContainer("restaurant-orders");
        _outboxContainer = database.GetContainer("restaurant-outbox");
    }

    /// <inheritdoc />
    public async Task<RestaurantOrder?> GetByIdAsync(Guid orderId, Guid restaurantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @orderId")
                .WithParameter("@orderId", orderId.ToString());

            var iterator = _ordersContainer.GetItemQueryIterator<RestaurantOrder>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(restaurantId.ToString())
                });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var order = response.FirstOrDefault();
                if (order is not null)
                {
                    order.ETag = response.ETag;
                    return order;
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
    public async Task<List<RestaurantOrder>> GetByRestaurantIdAsync(Guid restaurantId, RestaurantOrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        var queryText = "SELECT * FROM c WHERE c.restaurantId = @restaurantId";
        if (status.HasValue)
        {
            queryText += " AND c.status = @status";
        }

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@restaurantId", restaurantId.ToString());

        if (status.HasValue)
        {
            queryDefinition = queryDefinition.WithParameter("@status", (int)status.Value);
        }

        var iterator = _ordersContainer.GetItemQueryIterator<RestaurantOrder>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(restaurantId.ToString())
            });

        var results = new List<RestaurantOrder>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task SaveAsync(RestaurantOrder order, CancellationToken cancellationToken = default)
    {
        var partitionKey = new PartitionKey(order.RestaurantId.ToString());

        // Use a transactional batch to atomically persist the order and any outbox messages.
        var batch = _ordersContainer.CreateTransactionalBatch(partitionKey);

        // Upsert the order document
        batch.UpsertItem(order, new TransactionalBatchItemRequestOptions
        {
            IfMatchEtag = order.ETag
        });

        // Persist each domain event as an outbox message in the same partition
        foreach (var domainEvent in order.DomainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = order.RestaurantId.ToString(),
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
                $"Failed to save restaurant order '{order.Id}'. Cosmos DB transactional batch returned status code {batchResponse.StatusCode}.");
        }

        // Clear domain events after successful persistence
        order.ClearDomainEvents();
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid orderId, Guid restaurantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT c.id FROM c WHERE c.id = @orderId")
                .WithParameter("@orderId", orderId.ToString());

            var iterator = _ordersContainer.GetItemQueryIterator<dynamic>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(restaurantId.ToString()),
                    MaxItemCount = 1
                });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                if (response.Any())
                {
                    return true;
                }
            }

            return false;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
