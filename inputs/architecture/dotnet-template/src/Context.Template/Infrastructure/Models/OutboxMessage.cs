namespace {ContextName}.Infrastructure.Models;

/// <summary>
/// Represents an outbox message stored alongside the aggregate in the same
/// Cosmos DB transactional batch for reliable event publishing.
///
/// A background processor (Cosmos Change Feed) reads unprocessed messages
/// and publishes them to the appropriate Service Bus topic.
///
/// TTL is 7 days (604800 seconds). After processing, messages are marked
/// as Processed=true and eventually expire via Cosmos TTL.
/// </summary>
internal sealed class OutboxMessage
{
    /// <summary>Unique message identifier (GUID string).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Partition key — must match the aggregate's partition key
    /// for transactional batch atomicity.</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>Domain event type name (e.g., "OrderPlaced", "DriverAssigned").</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>JSON-serialized domain event payload.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the outbox message was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Whether this message has been published to Service Bus.</summary>
    public bool Processed { get; set; }

    /// <summary>Cosmos DB TTL in seconds. 7 days = 604800 seconds.</summary>
    public int Ttl { get; set; } = 604800;
}
