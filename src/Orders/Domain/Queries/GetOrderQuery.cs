using MediatR;

namespace Orders.Domain.Queries;

/// <summary>
/// Query to retrieve a single order by its unique identifier.
/// Dispatched from GetOrderFunction to the GetOrderQueryHandler via MediatR.
/// This is a read-only operation — no state changes or domain events are produced.
/// </summary>
public sealed record GetOrderQuery : IRequest<GetOrderResponse>
{
    /// <summary>
    /// Identifier of the order to retrieve.
    /// </summary>
    public required Guid OrderId { get; init; }
}

/// <summary>
/// Response DTO containing the full order details including line items,
/// current status, delivery address, financial totals, and all state
/// transition timestamps.
/// </summary>
public sealed record GetOrderResponse
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required Guid CustomerId { get; init; }
    public required Guid RestaurantId { get; init; }
    public required string Status { get; init; }
    public required List<GetOrderLineItemResponse> LineItems { get; init; }
    public required GetOrderDeliveryAddressResponse? DeliveryAddress { get; init; }
    public required GetOrderTotalResponse? OrderTotal { get; init; }
    public required GetOrderTimestampsResponse Timestamps { get; init; }
}

/// <summary>
/// Line item details within a GetOrderResponse.
/// </summary>
public sealed record GetOrderLineItemResponse
{
    public required Guid MenuItemId { get; init; }
    public required string MenuItemName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}

/// <summary>
/// Delivery address details within a GetOrderResponse.
/// </summary>
public sealed record GetOrderDeliveryAddressResponse
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string Postcode { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
}

/// <summary>
/// Financial total details within a GetOrderResponse.
/// </summary>
public sealed record GetOrderTotalResponse
{
    public required decimal Subtotal { get; init; }
    public required decimal DeliveryFee { get; init; }
    public required decimal Total { get; init; }
}

/// <summary>
/// All state transition timestamps for an order. Null timestamps indicate
/// that the order has not yet reached that lifecycle stage.
/// </summary>
public sealed record GetOrderTimestampsResponse
{
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset? PlacedAt { get; init; }
    public required DateTimeOffset? AcceptedAt { get; init; }
    public required DateTimeOffset? PreparingAt { get; init; }
    public required DateTimeOffset? ReadyForPickupAt { get; init; }
    public required DateTimeOffset? InDeliveryAt { get; init; }
    public required DateTimeOffset? DeliveredAt { get; init; }
    public required DateTimeOffset? RejectedAt { get; init; }
    public required DateTimeOffset? CancelledAt { get; init; }
}
