using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Restaurant.Domain.Aggregates;
using Restaurant.Infrastructure.Models;

namespace Restaurant.Functions.EventHandlers;

/// <summary>
/// Azure Function Cosmos DB Change Feed trigger that projects RestaurantOrder changes
/// to a dashboard-optimised read model in the dashboard-orders container.
/// This is a CQRS read-side projection — no domain events are raised.
/// RST-009.
/// </summary>
public sealed class ActiveOrderDashboardFunction
{
    private readonly Container _dashboardContainer;
    private readonly ILogger<ActiveOrderDashboardFunction> _logger;

    public ActiveOrderDashboardFunction(
        CosmosClient cosmosClient,
        ILogger<ActiveOrderDashboardFunction> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var database = cosmosClient.GetDatabase("swarmeats");
        _dashboardContainer = database.GetContainer("dashboard-orders");
    }

    [Function("ActiveOrderDashboard")]
    public async Task Run(
        [CosmosDBTrigger(
            databaseName: "swarmeats",
            containerName: "restaurant-orders",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "restaurant-orders-leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<RestaurantOrderDocument> input,
        FunctionContext context)
    {
        if (input == null || input.Count == 0)
        {
            return;
        }

        _logger.LogInformation("ActiveOrderDashboard processing {Count} changed documents.", input.Count);

        foreach (var document in input)
        {
            try
            {
                var projection = new DashboardOrderProjection
                {
                    Id = document.Id,
                    RestaurantId = document.RestaurantId,
                    OrderNumber = document.OrderNumber,
                    Status = document.Status,
                    EstimatedPrepTime = document.EstimatedPrepTime,
                    ReceivedAt = document.ReceivedAt,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    LineItems = document.LineItems?.Select(li => new DashboardLineItem
                    {
                        MenuItemName = li.MenuItemName,
                        Quantity = li.Quantity,
                        UnitPrice = li.UnitPrice
                    }).ToList() ?? new List<DashboardLineItem>()
                };

                await _dashboardContainer.UpsertItemAsync(
                    projection,
                    new PartitionKey(projection.RestaurantId));

                _logger.LogInformation(
                    "Dashboard projection updated for order: {OrderId}, status: {Status}",
                    document.Id, document.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to project dashboard for order: {OrderId}. Will retry on next change feed batch.",
                    document.Id);
            }
        }
    }
}

/// <summary>
/// Document model for RestaurantOrder changes received via the Cosmos DB Change Feed.
/// Used as the input binding type for the change feed trigger.
/// </summary>
public sealed class RestaurantOrderDocument
{
    public string Id { get; set; } = string.Empty;
    public string RestaurantId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? EstimatedPrepTime { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? PreparingAt { get; set; }
    public DateTimeOffset? ReadyAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public List<RestaurantOrderDocumentLineItem>? LineItems { get; set; }
}

/// <summary>
/// Line item within a RestaurantOrderDocument.
/// </summary>
public sealed class RestaurantOrderDocumentLineItem
{
    public string MenuItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
