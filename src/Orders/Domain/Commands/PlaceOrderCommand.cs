using MediatR;

namespace Orders.Domain.Commands;

/// <summary>
/// Command to place a new order. Validates business rules ORD-R01 through ORD-R04,
/// calculates order total (ORD-R06), generates order number (ORD-R07), and raises
/// the OrderPlaced domain event. Dispatched from PlaceOrderFunction via MediatR.
/// </summary>
public sealed record PlaceOrderCommand : IRequest<PlaceOrderResult>
{
    /// <summary>
    /// Identifier of the customer placing the order.
    /// </summary>
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// Identifier of the restaurant to order from.
    /// </summary>
    public required Guid RestaurantId { get; init; }

    /// <summary>
    /// Delivery address for the order.
    /// </summary>
    public required PlaceOrderDeliveryAddress DeliveryAddress { get; init; }

    /// <summary>
    /// Line items to include in the order.
    /// </summary>
    public required List<PlaceOrderLineItem> LineItems { get; init; }
}

/// <summary>
/// Delivery address data from the PlaceOrder request.
/// </summary>
public sealed record PlaceOrderDeliveryAddress
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
    /// Latitude coordinate. Required for delivery zone validation (ORD-R03).
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude coordinate. Required for delivery zone validation (ORD-R03).
    /// </summary>
    public double? Longitude { get; init; }
}

/// <summary>
/// Line item data from the PlaceOrder request.
/// </summary>
public sealed record PlaceOrderLineItem
{
    /// <summary>
    /// Menu item identifier.
    /// </summary>
    public required Guid MenuItemId { get; init; }

    /// <summary>
    /// Display name of the menu item.
    /// </summary>
    public required string MenuItemName { get; init; }

    /// <summary>
    /// Quantity ordered. Must be >= 1.
    /// </summary>
    public required int Quantity { get; init; }

    /// <summary>
    /// Unit price per item. Must be > 0.00.
    /// </summary>
    public required decimal UnitPrice { get; init; }
}

/// <summary>
/// Result returned on successful order placement.
/// </summary>
public sealed record PlaceOrderResult
{
    /// <summary>
    /// Unique order identifier.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// Human-readable order number (format: ORD-YYYYMMDD-sequential).
    /// </summary>
    public required string OrderNumber { get; init; }

    /// <summary>
    /// Order status after placement (always "Placed").
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Calculated order total including delivery fee.
    /// </summary>
    public required decimal OrderTotal { get; init; }
}
