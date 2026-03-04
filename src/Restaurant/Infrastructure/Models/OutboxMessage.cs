namespace Restaurant.Infrastructure.Models;

/// <summary>
/// Represents an outbox message stored alongside the aggregate for reliable event publishing.
/// TTL is 7 days. A background processor reads unprocessed messages and publishes to Service Bus.
/// Stored in the restaurant-outbox Cosmos DB container, partition key: /restaurantId.
/// </summary>
internal sealed class OutboxMessage
{
    public string Id { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool Processed { get; set; }

    /// <summary>
    /// Cosmos DB TTL in seconds. 7 days = 604800 seconds.
    /// </summary>
    public int Ttl { get; set; } = 604800;
}
