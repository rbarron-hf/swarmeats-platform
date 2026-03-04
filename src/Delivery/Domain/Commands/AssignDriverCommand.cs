using MediatR;

namespace Delivery.Domain.Commands;

/// <summary>
/// Command to assign a driver to a delivery awaiting a driver.
/// Validates DLV-R01 (driver availability) and DLV-R02 (driver within 5km).
/// Dispatched from AssignDriverFunction to the AssignDriverCommandHandler via MediatR.
/// </summary>
public sealed record AssignDriverCommand : IRequest<AssignDriverResult>
{
    /// <summary>
    /// Identifier of the delivery to assign a driver to.
    /// </summary>
    public required Guid DeliveryId { get; init; }

    /// <summary>
    /// Identifier of the driver being assigned.
    /// </summary>
    public required Guid DriverId { get; init; }

    /// <summary>
    /// Driver's current latitude.
    /// </summary>
    public required double DriverLatitude { get; init; }

    /// <summary>
    /// Driver's current longitude.
    /// </summary>
    public required double DriverLongitude { get; init; }
}

/// <summary>
/// Result returned on successful driver assignment.
/// </summary>
public sealed record AssignDriverResult
{
    public required Guid DeliveryId { get; init; }
    public required Guid OrderId { get; init; }
    public required string Status { get; init; }
    public required Guid DriverId { get; init; }
    public required AssignDriverLocationResponse DriverLocation { get; init; }
    public required AssignDriverRouteResponse Route { get; init; }
    public required AssignDriverEstimatedArrivalResponse EstimatedArrival { get; init; }
}

/// <summary>
/// Driver location details within an AssignDriverResult.
/// </summary>
public sealed record AssignDriverLocationResponse
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }
}

/// <summary>
/// Route details within an AssignDriverResult.
/// </summary>
public sealed record AssignDriverRouteResponse
{
    public required AssignDriverAddressResponse Pickup { get; init; }
    public required AssignDriverAddressResponse Dropoff { get; init; }
}

/// <summary>
/// Address details within route response.
/// </summary>
public sealed record AssignDriverAddressResponse
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string Postcode { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
}

/// <summary>
/// Estimated arrival details within an AssignDriverResult.
/// </summary>
public sealed record AssignDriverEstimatedArrivalResponse
{
    public required DateTimeOffset EstimatedArrivalTime { get; init; }
    public required int EstimatedMinutes { get; init; }
}
