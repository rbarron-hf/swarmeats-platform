using MediatR;

namespace Restaurant.Domain.Commands;

/// <summary>
/// Command to accept an incoming restaurant order with an estimated preparation time.
/// Dispatched from AcceptOrderFunction to the AcceptOrderCommandHandler via MediatR.
/// Enforces RST-R03 (prep time 5-90 min). RST-004.
/// </summary>
public sealed record AcceptOrderCommand : IRequest<AcceptOrderResult>
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
    /// Estimated preparation time in minutes (5-90 per RST-R03).
    /// </summary>
    public required int EstimatedPrepMinutes { get; init; }
}

/// <summary>
/// Result returned on successful order acceptance.
/// </summary>
public sealed record AcceptOrderResult
{
    public required Guid OrderId { get; init; }
    public required Guid RestaurantId { get; init; }
    public required string Status { get; init; }
    public required int EstimatedPrepMinutes { get; init; }
    public required DateTimeOffset AcceptedAt { get; init; }
}
