namespace Orders.Domain.Events;

/// <summary>
/// Domain event published to Service Bus topic orders.placed when a customer places an order.
/// Consumed by Restaurant context to create a RestaurantOrder and indirectly by Delivery context.
/// </summary>
public sealed record OrderPlaced : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Unique order identifier.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// Human-readable order number (format: ORD-YYYYMMDD-sequential).
    /// </summary>
    public required string OrderNumber { get; init; }

    /// <summary>
    /// Identifier of the customer who placed the order.
    /// </summary>
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// Identifier of the restaurant fulfilling the order.
    /// </summary>
    public required Guid RestaurantId { get; init; }

    /// <summary>
    /// Line items included in the order.
    /// </summary>
    public required List<OrderPlacedLineItem> LineItems { get; init; }

    /// <summary>
    /// Delivery address for the order.
    /// </summary>
    public required OrderPlacedDeliveryAddress DeliveryAddress { get; init; }

    /// <summary>
    /// Calculated order total including delivery fee.
    /// </summary>
    public required decimal OrderTotal { get; init; }

    /// <summary>
    /// Timestamp when the order was placed.
    /// </summary>
    public required DateTimeOffset PlacedAt { get; init; }
}

/// <summary>
/// Line item data included in the OrderPlaced event payload.
/// </summary>
public sealed record OrderPlacedLineItem
{
    /// <summary>
    /// Menu item identifier referenced by this line item.
    /// </summary>
    public required Guid MenuItemId { get; init; }

    /// <summary>
    /// Display name of the menu item.
    /// </summary>
    public required string MenuItemName { get; init; }

    /// <summary>
    /// Quantity ordered.
    /// </summary>
    public required int Quantity { get; init; }

    /// <summary>
    /// Unit price at the time of ordering.
    /// </summary>
    public required decimal UnitPrice { get; init; }
}

/// <summary>
/// Delivery address data included in the OrderPlaced event payload.
/// </summary>
public sealed record OrderPlacedDeliveryAddress
{
    /// <summary>
    /// Street address.
    /// </summary>
    public required string Street { get; init; }

    /// <summary>
    /// City name.
    /// </summary>
    public required string City { get; init; }

    /// <summary>
    /// Postal code.
    /// </summary>
    public required string Postcode { get; init; }

    /// <summary>
    /// Latitude coordinate of the delivery address.
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Longitude coordinate of the delivery address.
    /// </summary>
    public required double Longitude { get; init; }
}
