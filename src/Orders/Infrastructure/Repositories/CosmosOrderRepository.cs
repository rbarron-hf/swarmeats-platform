using System.Net;
using Microsoft.Azure.Cosmos;
using Orders.Domain.Aggregates;
using Orders.Infrastructure.Models;

namespace Orders.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation of IOrderRepository.
/// Uses the orders container with customerId as partition key.
/// Persists domain events to the outbox within the same transactional batch.
/// </summary>
public sealed class CosmosOrderRepository : IOrderRepository
{
    private readonly Container _ordersContainer;
    private readonly Container _outboxContainer;

    public CosmosOrderRepository(CosmosClient cosmosClient, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var database = cosmosClient.GetDatabase(databaseName);
        _ordersContainer = database.GetContainer("orders");
        _outboxContainer = database.GetContainer("orders-outbox");
    }

    /// <inheritdoc />
    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Cross-partition query since we may not know the customerId at lookup time.
            // For production, consider a secondary index or passing customerId as a parameter.
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @orderId")
                .WithParameter("@orderId", orderId.ToString());

            var iterator = _ordersContainer.GetItemQueryIterator<Order>(query);

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
    public async Task<(List<Order> Orders, string? ContinuationToken)> GetByCustomerIdAsync(
        Guid customerId,
        string? continuationToken = null,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.CustomerId = @customerId ORDER BY c.CreatedAt DESC")
            .WithParameter("@customerId", customerId.ToString());

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(customerId.ToString()),
            MaxItemCount = pageSize
        };

        var iterator = _ordersContainer.GetItemQueryIterator<Order>(
            query,
            continuationToken: continuationToken,
            requestOptions: requestOptions);

        var orders = new List<Order>();
        string? nextContinuationToken = null;

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            orders.AddRange(response);
            nextContinuationToken = response.ContinuationToken;
        }

        return (orders, nextContinuationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        var partitionKey = new PartitionKey(order.CustomerId.ToString());

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
                PartitionKey = order.CustomerId.ToString(),
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
                $"Failed to save order '{order.Id}'. Cosmos DB transactional batch returned status code {batchResponse.StatusCode}.");
        }

        // Clear domain events after successful persistence
        order.ClearDomainEvents();
    }
}
