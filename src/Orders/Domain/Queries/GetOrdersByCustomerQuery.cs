using MediatR;

namespace Orders.Domain.Queries;

/// <summary>
/// Query to retrieve all orders for a customer, sorted by creation date descending.
/// Supports pagination via continuation tokens.
/// Dispatched from GetOrdersByCustomerFunction to the GetOrdersByCustomerQueryHandler via MediatR.
/// This is a read-only operation -- no state changes or domain events are produced.
/// </summary>
public sealed record GetOrdersByCustomerQuery : IRequest<GetOrdersByCustomerResponse>
{
    /// <summary>
    /// Identifier of the customer whose orders to retrieve.
    /// </summary>
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// Optional continuation token for paginated results.
    /// </summary>
    public string? ContinuationToken { get; init; }
}

/// <summary>
/// Response DTO containing a paginated list of order summaries for a customer.
/// </summary>
public sealed record GetOrdersByCustomerResponse
{
    /// <summary>
    /// List of order summaries for the customer.
    /// </summary>
    public required List<OrderSummaryResponse> Orders { get; init; }

    /// <summary>
    /// Continuation token for the next page of results, or null if no more results.
    /// </summary>
    public string? ContinuationToken { get; init; }
}

/// <summary>
/// Summary DTO for an individual order within the GetOrdersByCustomerResponse.
/// Contains only the fields needed for a list view, not the full order details.
/// </summary>
public sealed record OrderSummaryResponse
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
    /// Name of the restaurant fulfilling the order.
    /// </summary>
    public required string RestaurantName { get; init; }

    /// <summary>
    /// Order total including delivery fee.
    /// </summary>
    public required decimal Total { get; init; }

    /// <summary>
    /// Current order status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Timestamp when the order was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
