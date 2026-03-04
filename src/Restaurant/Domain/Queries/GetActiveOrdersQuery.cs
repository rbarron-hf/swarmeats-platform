using MediatR;
using Restaurant.Domain.ValueObjects;

namespace Restaurant.Domain.Queries;

/// <summary>
/// Query to retrieve active orders for a restaurant, optionally filtered by status.
/// Dispatched from GetActiveOrdersFunction to the GetActiveOrdersQueryHandler via MediatR.
/// This is a read-only operation — no state changes or domain events are produced.
/// RST-002.
/// </summary>
public sealed record GetActiveOrdersQuery : IRequest<GetActiveOrdersResponse>
{
    /// <summary>
    /// The restaurant identifier.
    /// </summary>
    public required Guid RestaurantId { get; init; }

    /// <summary>
    /// Optional status filter. If null, returns all orders for the restaurant.
    /// </summary>
    public RestaurantOrderStatus? Status { get; init; }
}

/// <summary>
/// Response DTO containing the list of restaurant order summaries.
/// </summary>
public sealed record GetActiveOrdersResponse
{
    public required List<GetActiveOrderSummaryResponse> Orders { get; init; }
}

/// <summary>
/// Order summary within a GetActiveOrdersResponse.
/// </summary>
public sealed record GetActiveOrderSummaryResponse
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required string Status { get; init; }
    public required List<GetActiveOrderLineItemResponse> LineItems { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Line item details within an order summary.
/// </summary>
public sealed record GetActiveOrderLineItemResponse
{
    public required Guid MenuItemId { get; init; }
    public required string MenuItemName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}
