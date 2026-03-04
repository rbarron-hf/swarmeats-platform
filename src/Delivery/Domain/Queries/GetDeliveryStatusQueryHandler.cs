using MediatR;
using Delivery.Domain.Aggregates;
using Delivery.Domain.Exceptions;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Domain.Queries;

/// <summary>
/// Handles the GetDeliveryStatusQuery by loading the delivery aggregate from the repository
/// and mapping it to a GetDeliveryStatusResponse DTO. This is a pure read operation --
/// no business logic, no state changes, no domain events.
/// </summary>
public sealed class GetDeliveryStatusQueryHandler : IRequestHandler<GetDeliveryStatusQuery, GetDeliveryStatusResponse>
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetDeliveryStatusQueryHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<GetDeliveryStatusResponse> Handle(GetDeliveryStatusQuery request, CancellationToken cancellationToken)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(request.DeliveryId, cancellationToken);

        if (delivery is null)
        {
            throw new DeliveryNotFoundException(request.DeliveryId);
        }

        return MapToResponse(delivery);
    }

    private static GetDeliveryStatusResponse MapToResponse(DeliveryAggregate delivery)
    {
        return new GetDeliveryStatusResponse
        {
            DeliveryId = delivery.Id,
            OrderId = delivery.OrderId,
            Status = delivery.Status.ToString(),
            DriverId = delivery.DriverId,
            DriverLocation = delivery.DriverLocation is not null
                ? new GetDeliveryDriverLocationResponse
                {
                    Latitude = delivery.DriverLocation.Latitude,
                    Longitude = delivery.DriverLocation.Longitude,
                    RecordedAt = delivery.DriverLocation.UpdatedAt
                }
                : null,
            Route = new GetDeliveryRouteResponse
            {
                Pickup = new GetDeliveryAddressResponse
                {
                    Street = delivery.Route!.Pickup.Street,
                    City = delivery.Route.Pickup.City,
                    Postcode = delivery.Route.Pickup.Postcode,
                    Latitude = delivery.Route.Pickup.Latitude,
                    Longitude = delivery.Route.Pickup.Longitude
                },
                Dropoff = new GetDeliveryAddressResponse
                {
                    Street = delivery.Route.Dropoff.Street,
                    City = delivery.Route.Dropoff.City,
                    Postcode = delivery.Route.Dropoff.Postcode,
                    Latitude = delivery.Route.Dropoff.Latitude,
                    Longitude = delivery.Route.Dropoff.Longitude
                }
            },
            EstimatedArrival = delivery.EstimatedArrival is not null
                ? new GetDeliveryEstimatedArrivalResponse
                {
                    EstimatedArrivalTime = delivery.EstimatedArrival.EstimatedAt,
                    EstimatedMinutes = delivery.EstimatedArrival.EstimatedMinutes
                }
                : null,
            CreatedAt = delivery.CreatedAt
        };
    }
}
