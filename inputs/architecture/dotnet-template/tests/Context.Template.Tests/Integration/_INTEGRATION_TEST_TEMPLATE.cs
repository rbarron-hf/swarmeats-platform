using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Xunit;
using {ContextName}.Domain.Aggregates;
using {ContextName}.Infrastructure.Repositories;

namespace {ContextName}.Tests.Integration;

/// <summary>
/// Integration tests verifying Cosmos DB persistence for {Aggregate}.
///
/// Requires Cosmos DB Emulator running locally.
/// Connection string: AccountEndpoint=https://localhost:8081/;AccountKey=...
///
/// These tests verify:
/// - Round-trip serialization (write → read → compare)
/// - Partition key routing
/// - ETag-based optimistic concurrency
/// - Outbox message co-location in transactional batch
/// </summary>
[Collection("CosmosIntegration")]
public sealed class {Aggregate}RepositoryIntegrationTests : IAsyncLifetime
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;
    private readonly {Aggregate}Repository _repository;

    private const string DatabaseName = "swarmeats";
    private const string ContainerName = "{container-name}";

    public {Aggregate}RepositoryIntegrationTests()
    {
        _cosmosClient = new CosmosClient(
            "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });

        _container = _cosmosClient
            .GetDatabase(DatabaseName)
            .GetContainer(ContainerName);

        _repository = new {Aggregate}Repository(_cosmosClient);
    }

    public async Task InitializeAsync()
    {
        // Ensure database and container exist (emulator)
        var db = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
        await db.Database.CreateContainerIfNotExistsAsync(
            ContainerName, "/{partitionKey}");
    }

    public async Task DisposeAsync()
    {
        // Clean up test data — delete items created during tests
        // In production tests, use unique IDs and clean up per-test
        _cosmosClient.Dispose();
        await Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════════════
    // Round-trip persistence
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public async Task SaveAndRetrieve_{Aggregate}_RoundTripsCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var aggregate = new {Aggregate}(id /* , other params */);

        // Act
        await _repository.SaveAsync(aggregate);
        var retrieved = await _repository.GetByIdAsync(id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(id);
        // TODO: Assert all properties round-trip correctly
    }

    // ══════════════════════════════════════════════════════════════
    // Optimistic concurrency (ETag)
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Save_{Aggregate}WithStaleETag_ThrowsCosmosException()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var aggregate = new {Aggregate}(id /* , other params */);
        await _repository.SaveAsync(aggregate);

        // Load two copies
        var copy1 = await _repository.GetByIdAsync(id);
        var copy2 = await _repository.GetByIdAsync(id);

        // Modify and save the first copy
        copy1!.{TransitionMethod}(/* params */);
        await _repository.SaveAsync(copy1);

        // Act — saving copy2 should fail (stale ETag)
        copy2!.{TransitionMethod}(/* params */);
        var act = async () => await _repository.SaveAsync(copy2);

        // Assert
        await act.Should().ThrowAsync<CosmosException>()
            .Where(ex => ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed);
    }

    // ══════════════════════════════════════════════════════════════
    // Outbox messages
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Save_{Aggregate}WithDomainEvents_CreatesOutboxMessages()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        var aggregate = new {Aggregate}(id /* , other params */);
        // Trigger an action that raises a domain event
        aggregate.{TransitionMethod}(/* params */);

        // Act
        await _repository.SaveAsync(aggregate);

        // Assert — query outbox messages in same partition
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.type = 'outbox' AND c.partitionKey = @pk")
            .WithParameter("@pk", /* partition key value */);

        var iterator = _container.GetItemQueryIterator<dynamic>(query);
        var messages = new List<dynamic>();
        while (iterator.HasMoreResults)
        {
            var batch = await iterator.ReadNextAsync();
            messages.AddRange(batch);
        }

        messages.Should().ContainSingle(m =>
            m.eventType == "{EventName}");
    }
}
