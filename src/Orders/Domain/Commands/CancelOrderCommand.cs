using MediatR;

namespace Orders.Domain.Commands;

/// <summary>
/// Command to cancel an existing order. Only permitted while the order status is Placed (ORD-R05).
/// Dispatched from CancelOrderFunction to the CancelOrderCommandHandler via MediatR.
/// </summary>
public sealed record CancelOrderCommand : IRequest<CancelOrderResult>
{
    /// <summary>
    /// Identifier of the order to cancel.
    /// </summary>
    public required Guid OrderId { get; init; }
}

/// <summary>
/// Result returned on successful order cancellation.
/// </summary>
public sealed record CancelOrderResult
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CancelledAt { get; init; }
}
