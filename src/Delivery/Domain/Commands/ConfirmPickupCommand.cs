using MediatR;

namespace Delivery.Domain.Commands;

/// <summary>
/// Command to confirm that a driver has picked up the food from the restaurant.
/// Dispatched from ConfirmPickupFunction to the handler via MediatR.
/// </summary>
public sealed record ConfirmPickupCommand : IRequest<ConfirmPickupResult>
{
    /// <summary>
    /// Identifier of the delivery.
    /// </summary>
    public required Guid DeliveryId { get; init; }

    /// <summary>
    /// Identifier of the driver confirming pickup.
    /// </summary>
    public required Guid DriverId { get; init; }
}

/// <summary>
/// Result returned on successful pickup confirmation.
/// </summary>
public sealed record ConfirmPickupResult
{
    public required Guid DeliveryId { get; init; }
    public required Guid OrderId { get; init; }
    public required string Status { get; init; }
    public required Guid DriverId { get; init; }
    public required ConfirmPickupLocationResponse? DriverLocation { get; init; }
    public required ConfirmPickupRouteResponse Route { get; init; }
    public required DateTimeOffset PickedUpAt { get; init; }
}

/// <summary>
/// Driver location details within a ConfirmPickupResult.
/// </summary>
public sealed record ConfirmPickupLocationResponse
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required DateTimeOffset RecordedAt { get; init; }
}

/// <summary>
/// Route details within a ConfirmPickupResult.
/// </summary>
public sealed record ConfirmPickupRouteResponse
{
    public required ConfirmPickupAddressResponse Pickup { get; init; }
    public required ConfirmPickupAddressResponse Dropoff { get; init; }
}

/// <summary>
/// Address details within route response.
/// </summary>
public sealed record ConfirmPickupAddressResponse
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string Postcode { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
}
