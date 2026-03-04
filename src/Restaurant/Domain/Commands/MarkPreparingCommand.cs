using MediatR;

namespace Restaurant.Domain.Commands;

/// <summary>
/// Command to mark a restaurant order as being prepared.
/// Dispatched from UpdatePreparationStatusFunction to the MarkPreparingCommandHandler via MediatR.
/// State transition: Accepted -> Preparing. No domain event raised.
/// RST-006.
/// </summary>
public sealed record MarkPreparingCommand : IRequest<MarkPreparingResult>
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
/// Result returned on successful status transition to Preparing.
/// </summary>
public sealed record MarkPreparingResult
{
    public required Guid OrderId { get; init; }
    public required Guid RestaurantId { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
