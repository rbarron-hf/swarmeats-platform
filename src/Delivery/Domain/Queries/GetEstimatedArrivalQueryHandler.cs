using MediatR;
using Delivery.Domain.Exceptions;
using Delivery.Domain.ValueObjects;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Domain.Queries;

/// <summary>
/// Handles the GetEstimatedArrivalQuery by loading the delivery aggregate,
/// validating that a driver is assigned, and returning the pre-calculated
/// estimated arrival. This is a pure read operation.
/// </summary>
public sealed class GetEstimatedArrivalQueryHandler : IRequestHandler<GetEstimatedArrivalQuery, GetEstimatedArrivalResponse>
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetEstimatedArrivalQueryHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<GetEstimatedArrivalResponse> Handle(GetEstimatedArrivalQuery request, CancellationToken cancellationToken)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(request.DeliveryId, cancellationToken);

        if (delivery is null)
        {
            throw new DeliveryNotFoundException(request.DeliveryId);
        }

        // If no driver is assigned, ETA is not available
        if (delivery.Status == DeliveryStatus.AwaitingDriver || delivery.EstimatedArrival is null || delivery.DriverLocation is null)
        {
            throw new InvalidDeliveryStateException(
                delivery.Id, delivery.Status, "DELIVERY_NO_DRIVER",
                $"Delivery '{delivery.Id}' does not have a driver assigned yet. Estimated arrival is not available.");
        }

        return new GetEstimatedArrivalResponse
        {
            DeliveryId = delivery.Id,
            EstimatedArrivalTime = delivery.EstimatedArrival.EstimatedAt,
            EstimatedMinutes = delivery.EstimatedArrival.EstimatedMinutes,
            LastLocationUpdate = delivery.DriverLocation.UpdatedAt
        };
    }
}
