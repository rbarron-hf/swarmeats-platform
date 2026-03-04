using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace {ContextName}.Functions.EventHandlers;

/// <summary>
/// Cosmos DB Change Feed trigger that projects {SourceContainer} changes
/// into the {ProjectionContainer} read model.
///
/// Source container: {source-container}
/// Projection container: {projection-container}
/// Story: {STORY_ID}
///
/// Projections are upserted (not inserted) for idempotency since the
/// change feed may redeliver documents.
/// </summary>
public sealed class {FunctionName}
{
    private readonly Container _projectionContainer;
    private readonly ILogger<{FunctionName}> _logger;

    public {FunctionName}(
        CosmosClient cosmosClient,
        ILogger<{FunctionName}> logger)
    {
        _projectionContainer = cosmosClient
            .GetDatabase("swarmeats")
            .GetContainer("{projection-container}");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("{FunctionName}")]
    public async Task Run(
        [CosmosDBTrigger(
            databaseName: "swarmeats",
            containerName: "{source-container}",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<{DocumentType}> changes,
        FunctionContext context)
    {
        if (changes is null || changes.Count == 0) return;

        _logger.LogInformation(
            "Processing {Count} change feed documents from {Container}",
            changes.Count, "{source-container}");

        foreach (var document in changes)
        {
            try
            {
                var projection = MapToProjection(document);

                await _projectionContainer.UpsertItemAsync(
                    projection,
                    new PartitionKey(projection.{PartitionKeyProperty}));
            }
            catch (Exception ex)
            {
                // Log but do NOT throw — change feed retries from checkpoint
                // on next invocation if the function throws
                _logger.LogError(ex,
                    "Failed to project document {Id}. Skipping.",
                    document.Id);
            }
        }
    }

    private static {ProjectionType} MapToProjection({DocumentType} document)
    {
        return new {ProjectionType}
        {
            // TODO: Map source document to projection read model
            Id = document.Id,
            ProjectedAt = DateTimeOffset.UtcNow
        };
    }
}
