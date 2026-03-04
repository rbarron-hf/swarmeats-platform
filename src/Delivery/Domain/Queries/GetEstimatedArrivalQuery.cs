using MediatR;

namespace Delivery.Domain.Queries;

/// <summary>
/// Query to retrieve the estimated arrival time for a delivery.
/// Requires a driver to be assigned. Dispatched from GetEstimatedArrivalFunction
/// to the handler via MediatR.
/// This is a read-only operation -- no state changes or domain events are produced.
/// </summary>
public sealed record GetEstimatedArrivalQuery : IRequest<GetEstimatedArrivalResponse>
{
    /// <summary>
    /// Identifier of the delivery to query.
    /// </summary>
    public required Guid DeliveryId { get; init; }
}

/// <summary>
/// Response DTO containing the estimated arrival time and last location update.
/// </summary>
public sealed record GetEstimatedArrivalResponse
{
    public required Guid DeliveryId { get; init; }
    public required DateTimeOffset EstimatedArrivalTime { get; init; }
    public required int EstimatedMinutes { get; init; }
    public required DateTimeOffset LastLocationUpdate { get; init; }
}
