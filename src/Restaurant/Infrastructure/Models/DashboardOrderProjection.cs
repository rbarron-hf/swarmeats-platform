namespace Restaurant.Infrastructure.Models;

/// <summary>
/// Read-optimised projection of a RestaurantOrder for the restaurant dashboard (RST-009).
/// Stored in the dashboard-orders Cosmos DB container.
/// Denormalised for fast queries by restaurant staff.
/// </summary>
public sealed class DashboardOrderProjection
{
    /// <summary>
    /// Unique identifier (same as the RestaurantOrder/Order ID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Restaurant identifier. Also serves as the Cosmos DB partition key.
    /// </summary>
    public string RestaurantId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable order number.
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Current order status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Denormalised line item summaries.
    /// </summary>
    public List<DashboardLineItem> LineItems { get; set; } = new();

    /// <summary>
    /// Estimated preparation time in minutes (null if not yet accepted).
    /// </summary>
    public int? EstimatedPrepTime { get; set; }

    /// <summary>
    /// Timestamp when the order was received.
    /// </summary>
    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>
    /// Timestamp of the most recent status change.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Cosmos DB TTL in seconds. 30 days = 2592000 seconds.
    /// </summary>
    public int Ttl { get; set; } = 2592000;
}

/// <summary>
/// Simplified line item for the dashboard projection.
/// </summary>
public sealed class DashboardLineItem
{
    public string MenuItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
