namespace {ContextName}.Domain.Events.Consumed;

/// <summary>
/// Event envelope received from the {SourceContext} context via Service Bus.
/// Topic: {topic.name}, Subscription: {context}-subscription
///
/// This is an anti-corruption layer DTO. It maps the external event schema
/// to internal domain concepts. If the upstream schema changes, only this
/// file needs to be updated.
/// </summary>
public sealed class {EventName}Event
{
    /// <summary>
    /// Type discriminator for the event (e.g., "OrderPlaced").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for idempotency checks.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the event was originally raised.
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// Name of the bounded context that raised this event.
    /// </summary>
    public string SourceContext { get; set; } = string.Empty;

    /// <summary>
    /// Event-specific payload. Null if deserialization fails partially.
    /// </summary>
    public {EventName}Payload? Payload { get; set; }
}

/// <summary>
/// Payload of the {EventName} event.
/// </summary>
public sealed class {EventName}Payload
{
    // TODO: Add fields matching the upstream event's data contract.
    // Use nullable types for optional fields.
    // Example:
    //   public Guid OrderId { get; set; }
    //   public string OrderNumber { get; set; } = string.Empty;
    //   public DateTimeOffset PlacedAt { get; set; }
}
