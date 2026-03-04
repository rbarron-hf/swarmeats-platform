using Microsoft.Azure.Cosmos;
using Delivery.Domain.Aggregates;
using Delivery.Domain.ValueObjects;

namespace Delivery.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation of IDriverRepository.
/// Queries the deliveries container to check whether a driver has an active delivery.
/// Enforces business rule DLV-R01.
/// </summary>
public sealed class CosmosDriverRepository : IDriverRepository
{
    private readonly Container _deliveriesContainer;

    public CosmosDriverRepository(CosmosClient cosmosClient, string databaseName)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var database = cosmosClient.GetDatabase(databaseName);
        _deliveriesContainer = database.GetContainer("deliveries");
    }

    /// <inheritdoc />
    public async Task<bool> HasActiveDeliveryAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        // Query the driver's partition for any delivery in DriverAssigned or PickedUp status
        var query = new QueryDefinition(
                "SELECT VALUE COUNT(1) FROM c WHERE c.driverId = @driverId AND (c.status = @driverAssigned OR c.status = @pickedUp)")
            .WithParameter("@driverId", driverId.ToString())
            .WithParameter("@driverAssigned", (int)DeliveryStatus.DriverAssigned)
            .WithParameter("@pickedUp", (int)DeliveryStatus.PickedUp);

        var iterator = _deliveriesContainer.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(driverId.ToString())
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var count = response.FirstOrDefault();
            if (count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
