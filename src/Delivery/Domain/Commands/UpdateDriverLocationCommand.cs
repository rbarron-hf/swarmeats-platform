using MediatR;

namespace Delivery.Domain.Commands;

/// <summary>
/// Command to update a driver's location during an active delivery.
/// Dispatched from UpdateDriverLocationFunction to the handler via MediatR.
/// </summary>
public sealed record UpdateDriverLocationCommand : IRequest<UpdateDriverLocationResult>
{
    /// <summary>
    /// Identifier of the delivery being tracked.
    /// </summary>
    public required Guid DeliveryId { get; init; }

    /// <summary>
    /// Identifier of the driver reporting their location.
    /// </summary>
    public required Guid DriverId { get; init; }

    /// <summary>
    /// Driver's current latitude.
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Driver's current longitude.
    /// </summary>
    public required double Longitude { get; init; }
}

/// <summary>
/// Result returned on successful driver location update.
/// </summary>
public sealed record UpdateDriverLocationResult
{
    public required Guid DeliveryId { get; init; }
    public required UpdateDriverLocationEstimatedArrivalResponse EstimatedArrival { get; init; }
    public required DateTimeOffset LastLocationUpdate { get; init; }
}

/// <summary>
/// Estimated arrival details within an UpdateDriverLocationResult.
/// </summary>
public sealed record UpdateDriverLocationEstimatedArrivalResponse
{
    public required DateTimeOffset EstimatedArrivalTime { get; init; }
    public required int EstimatedMinutes { get; init; }
}
