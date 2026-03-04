using MediatR;

namespace Restaurant.Domain.Commands;

/// <summary>
/// Command to mark a restaurant order as ready for driver pickup.
/// Dispatched from UpdatePreparationStatusFunction to the MarkReadyForPickupCommandHandler via MediatR.
/// State transition: Preparing -> ReadyForPickup. Raises OrderReadyForPickup event.
/// RST-007.
/// </summary>
public sealed record MarkReadyForPickupCommand : IRequest<MarkReadyForPickupResult>
{
    /// <summary>
    /// The order identifier.
    /// </summary>
    public required Guid OrderId { get; init; }

    /// <summary>
    /// The restaurant identifier (partition key).
    /// </summary>
    public required Guid RestaurantId { get; init; }
}

/// <summary>
/// Result returned on successful status transition to ReadyForPickup.
/// </summary>
public sealed record MarkReadyForPickupResult
{
    public required Guid OrderId { get; init; }
    public required Guid RestaurantId { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset ReadyAt { get; init; }
}
