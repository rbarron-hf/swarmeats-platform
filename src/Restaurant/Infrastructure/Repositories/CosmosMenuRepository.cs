using System.Net;
using Microsoft.Azure.Cosmos;
using Restaurant.Domain.Aggregates;

namespace Restaurant.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation of IMenuRepository.
/// Uses the menus container with restaurantId as partition key.
/// Read-only repository — menus are managed externally.
/// </summary>
public sealed class CosmosMenuRepository : IMenuRepository
{
    private readonly Container _menusContainer;

    public CosmosMenuRepository(CosmosClient cosmosClient, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var database = cosmosClient.GetDatabase(databaseName);
        _menusContainer = database.GetContainer("menus");
    }

    /// <inheritdoc />
    public async Task<Menu?> GetByRestaurantIdAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.restaurantId = @restaurantId")
                .WithParameter("@restaurantId", restaurantId.ToString());

            var iterator = _menusContainer.GetItemQueryIterator<Menu>(
                query,
                requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(restaurantId.ToString())
                });

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var menu = response.FirstOrDefault();
                if (menu is not null)
                {
                    menu.ETag = response.ETag;
                    return menu;
                }
            }

            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
