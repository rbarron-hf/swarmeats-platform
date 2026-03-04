using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using {ContextName}.Domain.Aggregates;
using {ContextName}.Infrastructure.Models;

namespace {ContextName}.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation of <see cref="I{Aggregate}Repository"/>.
///
/// Container: {container-name}
/// Partition key: /{partitionKeyProperty}
///
/// Uses transactional batch to atomically persist:
///   1. The aggregate document (upsert with ETag)
///   2. One outbox message per uncommitted domain event
/// </summary>
public sealed class Cosmos{Aggregate}Repository : I{Aggregate}Repository
{
    private readonly Container _container;

    public Cosmos{Aggregate}Repository(CosmosClient cosmosClient, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var database = cosmosClient.GetDatabase(databaseName);
        _container = database.GetContainer("{container-name}");
    }

    /// <inheritdoc />
    public async Task<{AggregateType}?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Option A: Point read (requires known partition key)
            // var response = await _container.ReadItemAsync<{AggregateType}>(
            //     id.ToString(),
            //     new PartitionKey(partitionKeyValue),
            //     cancellationToken: cancellationToken);
            // response.Resource.ETag = response.ETag;
            // return response.Resource;

            // Option B: Cross-partition query (when partition key unknown)
            var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
                .WithParameter("@id", id.ToString());

            var iterator = _container.GetItemQueryIterator<{AggregateType}>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var item = response.FirstOrDefault();
                if (item is not null)
                {
                    item.ETag = response.ETag;
                    return item;
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
    public async Task SaveAsync({AggregateType} aggregate, CancellationToken cancellationToken = default)
    {
        var partitionKey = new PartitionKey(aggregate.{PartitionKeyProperty}.ToString());

        // Transactional batch: aggregate + outbox messages in one atomic operation
        var batch = _container.CreateTransactionalBatch(partitionKey);

        // Upsert aggregate with optimistic concurrency
        batch.UpsertItem(aggregate, new TransactionalBatchItemRequestOptions
        {
            IfMatchEtag = aggregate.ETag
        });

        // Persist each domain event as an outbox message
        foreach (var domainEvent in aggregate.DomainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = aggregate.{PartitionKeyProperty}.ToString(),
                EventType = domainEvent.GetType().Name,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                CreatedAt = DateTimeOffset.UtcNow,
                Processed = false
            };

            batch.CreateItem(outboxMessage);
        }

        var batchResponse = await batch.ExecuteAsync(cancellationToken);

        if (!batchResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to save {typeof({AggregateType}).Name} '{aggregate.Id}'. " +
                $"Cosmos DB transactional batch returned status code {batchResponse.StatusCode}.");
        }

        // Clear domain events after successful persistence
        aggregate.ClearDomainEvents();
    }
}
