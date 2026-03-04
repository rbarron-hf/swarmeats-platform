using MediatR;
using Delivery.Domain.Exceptions;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Domain.Commands;

/// <summary>
/// Handles the AssignDriverCommand by loading the delivery aggregate,
/// checking driver availability (DLV-R01), delegating to the aggregate's
/// AssignDriver() method (which enforces DLV-R02), and persisting the result.
/// Performs partition key migration from 'unassigned' to the driver's partition.
/// </summary>
public sealed class AssignDriverCommandHandler : IRequestHandler<AssignDriverCommand, AssignDriverResult>
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IDriverRepository _driverRepository;

    public AssignDriverCommandHandler(
        IDeliveryRepository deliveryRepository,
        IDriverRepository driverRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _driverRepository = driverRepository ?? throw new ArgumentNullException(nameof(driverRepository));
    }

    public async Task<AssignDriverResult> Handle(AssignDriverCommand request, CancellationToken cancellationToken)
    {
        // Load the delivery aggregate from the repository
        var delivery = await _deliveryRepository.GetByIdAsync(request.DeliveryId, cancellationToken);

        if (delivery is null)
        {
            throw new DeliveryNotFoundException(request.DeliveryId);
        }

        // DLV-R01: Check driver availability before delegating to aggregate
        var hasActiveDelivery = await _driverRepository.HasActiveDeliveryAsync(request.DriverId, cancellationToken);
        if (hasActiveDelivery)
        {
            throw new DriverNotAvailableException(request.DriverId);
        }

        // Delegate to the aggregate -- AssignDriver() enforces DLV-R02 (distance check) internally
        delivery.AssignDriver(request.DriverId, request.DriverLatitude, request.DriverLongitude);

        // Persist with partition key migration (delete from 'unassigned', recreate under driver partition)
        await _deliveryRepository.MigratePartitionKeyAsync(delivery, cancellationToken);

        return new AssignDriverResult
        {
            DeliveryId = delivery.Id,
            OrderId = delivery.OrderId,
            Status = delivery.Status.ToString(),
            DriverId = delivery.DriverId!.Value,
            DriverLocation = new AssignDriverLocationResponse
            {
                Latitude = delivery.DriverLocation!.Latitude,
                Longitude = delivery.DriverLocation.Longitude,
                RecordedAt = delivery.DriverLocation.UpdatedAt
            },
            Route = new AssignDriverRouteResponse
            {
                Pickup = new AssignDriverAddressResponse
                {
                    Street = delivery.Route!.Pickup.Street,
                    City = delivery.Route.Pickup.City,
                    Postcode = delivery.Route.Pickup.Postcode,
                    Latitude = delivery.Route.Pickup.Latitude,
                    Longitude = delivery.Route.Pickup.Longitude
                },
                Dropoff = new AssignDriverAddressResponse
                {
                    Street = delivery.Route.Dropoff.Street,
                    City = delivery.Route.Dropoff.City,
                    Postcode = delivery.Route.Dropoff.Postcode,
                    Latitude = delivery.Route.Dropoff.Latitude,
                    Longitude = delivery.Route.Dropoff.Longitude
                }
            },
            EstimatedArrival = new AssignDriverEstimatedArrivalResponse
            {
                EstimatedArrivalTime = delivery.EstimatedArrival!.EstimatedAt,
                EstimatedMinutes = delivery.EstimatedArrival.EstimatedMinutes
            }
        };
    }
}
