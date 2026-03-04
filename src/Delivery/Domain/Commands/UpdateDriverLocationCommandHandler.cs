using MediatR;
using Delivery.Domain.Exceptions;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Domain.Commands;

/// <summary>
/// Handles the UpdateDriverLocationCommand by loading the delivery aggregate,
/// delegating to the aggregate's UpdateDriverLocation() method, and persisting the result.
/// No business logic lives here -- all rules are enforced inside the aggregate.
/// </summary>
public sealed class UpdateDriverLocationCommandHandler : IRequestHandler<UpdateDriverLocationCommand, UpdateDriverLocationResult>
{
    private readonly IDeliveryRepository _deliveryRepository;

    public UpdateDriverLocationCommandHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<UpdateDriverLocationResult> Handle(UpdateDriverLocationCommand request, CancellationToken cancellationToken)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(request.DeliveryId, cancellationToken);

        if (delivery is null)
        {
            throw new DeliveryNotFoundException(request.DeliveryId);
        }

        // Delegate to the aggregate -- UpdateDriverLocation() enforces status and driver checks internally
        delivery.UpdateDriverLocation(request.DriverId, request.Latitude, request.Longitude);

        // Persist the updated aggregate
        await _deliveryRepository.SaveAsync(delivery, cancellationToken);

        return new UpdateDriverLocationResult
        {
            DeliveryId = delivery.Id,
            EstimatedArrival = new UpdateDriverLocationEstimatedArrivalResponse
            {
                EstimatedArrivalTime = delivery.EstimatedArrival!.EstimatedAt,
                EstimatedMinutes = delivery.EstimatedArrival.EstimatedMinutes
            },
            LastLocationUpdate = delivery.DriverLocation!.UpdatedAt
        };
    }
}
