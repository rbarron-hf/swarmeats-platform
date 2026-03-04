namespace Restaurant.Domain.Events.Consumed;

/// <summary>
/// Represents the OrderPlaced event received from the Orders context via Service Bus topic orders.placed.
/// This is the external event contract consumed by RST-003 (HandleOrderPlacedFunction).
/// </summary>
public sealed record OrderPlacedEvent
{
    /// <summary>
    /// The Service Bus message envelope event type.
    /// </summary>
    public string EventType { get; init; } = "OrderPlaced";

    /// <summary>
    /// Unique event identifier for idempotent processing.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Source bounded context name.
    /// </summary>
    public string SourceContext { get; init; } = "Orders";

    /// <summary>
    /// Event payload containing order details.
    /// </summary>
    public required OrderPlacedPayload Payload { get; init; }
}

/// <summary>
/// Payload of the OrderPlaced event containing all order details.
/// </summary>
public sealed record OrderPlacedPayload
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public Guid RestaurantId { get; init; }
    public List<OrderPlacedLineItem> LineItems { get; init; } = new();
    public OrderPlacedDeliveryAddress? DeliveryAddress { get; init; }
    public decimal OrderTotal { get; init; }
    public DateTimeOffset PlacedAt { get; init; }
}

/// <summary>
/// Line item within the OrderPlaced event payload.
/// </summary>
public sealed record OrderPlacedLineItem
{
    public Guid MenuItemId { get; init; }
    public string MenuItemName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

/// <summary>
/// Delivery address within the OrderPlaced event payload.
/// </summary>
public sealed record OrderPlacedDeliveryAddress
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Postcode { get; init; } = string.Empty;
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
}
