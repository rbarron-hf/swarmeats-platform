using MediatR;

namespace Delivery.Domain.Queries;

/// <summary>
/// Query to retrieve the full delivery status by its unique identifier.
/// Dispatched from GetDeliveryStatusFunction to the handler via MediatR.
/// This is a read-only operation -- no state changes or domain events are produced.
/// </summary>
public sealed record GetDeliveryStatusQuery : IRequest<GetDeliveryStatusResponse>
{
    /// <summary>
    /// Identifier of the delivery to retrieve.
    /// </summary>
    public required Guid DeliveryId { get; init; }
}

/// <summary>
/// Response DTO containing the full delivery details including status, driver location,
/// route, and estimated arrival.
/// </summary>
public sealed record GetDeliveryStatusResponse
{
    public required Guid DeliveryId { get; init; }
    public required Guid OrderId { get; init; }
    public required string Status { get; init; }
    public required Guid? DriverId { get; init; }
    public required GetDeliveryDriverLocationResponse? DriverLocation { get; init; }
    public required GetDeliveryRouteResponse Route { get; init; }
    public required GetDeliveryEstimatedArrivalResponse? EstimatedArrival { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Driver location details within a GetDeliveryStatusResponse.
/// </summary>
public sealed record GetDeliveryDriverLocationResponse
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }
}

/// <summary>
/// Route details within a GetDeliveryStatusResponse.
/// </summary>
public sealed record GetDeliveryRouteResponse
{
    public required GetDeliveryAddressResponse Pickup { get; init; }
    public required GetDeliveryAddressResponse Dropoff { get; init; }
}

/// <summary>
/// Address details within a route response.
/// </summary>
public sealed record GetDeliveryAddressResponse
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string Postcode { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
}

/// <summary>
/// Estimated arrival details within a GetDeliveryStatusResponse.
/// </summary>
public sealed record GetDeliveryEstimatedArrivalResponse
{
    public required DateTimeOffset EstimatedArrivalTime { get; init; }
    public required int EstimatedMinutes { get; init; }
}
