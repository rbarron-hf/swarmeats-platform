using MediatR;
using Delivery.Domain.Exceptions;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Domain.Commands;

/// <summary>
/// Handles the ConfirmPickupCommand by loading the delivery aggregate,
/// delegating to the aggregate's ConfirmPickup() method, and persisting the result.
/// No business logic lives here -- all rules are enforced inside the aggregate.
/// </summary>
public sealed class ConfirmPickupCommandHandler : IRequestHandler<ConfirmPickupCommand, ConfirmPickupResult>
{
    private readonly IDeliveryRepository _deliveryRepository;

    public ConfirmPickupCommandHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<ConfirmPickupResult> Handle(ConfirmPickupCommand request, CancellationToken cancellationToken)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(request.DeliveryId, cancellationToken);

        if (delivery is null)
        {
            throw new DeliveryNotFoundException(request.DeliveryId);
        }

        // Delegate to the aggregate -- ConfirmPickup() enforces status and driver checks internally
        delivery.ConfirmPickup(request.DriverId);

        // Persist the updated aggregate
        await _deliveryRepository.SaveAsync(delivery, cancellationToken);

        return new ConfirmPickupResult
        {
            DeliveryId = delivery.Id,
            OrderId = delivery.OrderId,
            Status = delivery.Status.ToString(),
            DriverId = delivery.DriverId!.Value,
            DriverLocation = delivery.DriverLocation is not null
                ? new ConfirmPickupLocationResponse
                {
                    Latitude = delivery.DriverLocation.Latitude,
                    Longitude = delivery.DriverLocation.Longitude,
                    RecordedAt = delivery.DriverLocation.UpdatedAt
                }
                : null,
            Route = new ConfirmPickupRouteResponse
            {
                Pickup = new ConfirmPickupAddressResponse
                {
                    Street = delivery.Route!.Pickup.Street,
                    City = delivery.Route.Pickup.City,
                    Postcode = delivery.Route.Pickup.Postcode,
                    Latitude = delivery.Route.Pickup.Latitude,
                    Longitude = delivery.Route.Pickup.Longitude
                },
                Dropoff = new ConfirmPickupAddressResponse
                {
                    Street = delivery.Route.Dropoff.Street,
                    City = delivery.Route.Dropoff.City,
                    Postcode = delivery.Route.Dropoff.Postcode,
                    Latitude = delivery.Route.Dropoff.Latitude,
                    Longitude = delivery.Route.Dropoff.Longitude
                }
            },
            PickedUpAt = delivery.PickedUpAt!.Value
        };
    }
}
