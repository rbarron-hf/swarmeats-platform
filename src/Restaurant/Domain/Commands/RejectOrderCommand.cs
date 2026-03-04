using MediatR;

namespace Restaurant.Domain.Commands;

/// <summary>
/// Command to reject an incoming restaurant order with a reason code and optional notes.
/// Dispatched from RejectOrderFunction to the RejectOrderCommandHandler via MediatR.
/// Enforces RST-R04 (valid reason codes) and RST-R05 (only from Pending). RST-005.
/// </summary>
public sealed record RejectOrderCommand : IRequest<RejectOrderResult>
{
    /// <summary>
    /// The order identifier.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// The restaurant identifier (partition key).
    /// </summary>
    public required Guid RestaurantId { get; init; }

    /// <summary>
    /// Rejection reason code from the allowed set: RESTAURANT_CLOSED, ITEM_UNAVAILABLE, TOO_BUSY, OTHER.
    /// </summary>
    public required string ReasonCode { get; init; }

    /// <summary>
    /// Optional notes from restaurant staff.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Result returned on successful order rejection.
/// </summary>
public sealed record RejectOrderResult
{
    public required Guid OrderId { get; init; }
    public required Guid RestaurantId { get; init; }
    public required string Status { get; init; }
    public required string ReasonCode { get; init; }
    public string? Notes { get; init; }
    public required DateTimeOffset RejectedAt { get; init; }
}
