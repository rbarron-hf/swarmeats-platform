using MediatR;
using Delivery.Domain.Exceptions;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Domain.Commands;

/// <summary>
/// Handles the CompleteDeliveryCommand by loading the delivery aggregate,
/// delegating to the aggregate's Complete() method, and persisting the result.
/// No business logic lives here -- all rules are enforced inside the aggregate.
/// </summary>
public sealed class CompleteDeliveryCommandHandler : IRequestHandler<CompleteDeliveryCommand, CompleteDeliveryResult>
{
    private readonly IDeliveryRepository _deliveryRepository;

    public CompleteDeliveryCommandHandler(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<CompleteDeliveryResult> Handle(CompleteDeliveryCommand request, CancellationToken cancellationToken)
    {
        var delivery = await _deliveryRepository.GetByIdAsync(request.DeliveryId, cancellationToken);

        if (delivery is null)
        {
            throw new DeliveryNotFoundException(request.DeliveryId);
        }

        // Delegate to the aggregate -- Complete() enforces status and driver checks internally
        delivery.Complete(request.DriverId);

        // Persist the updated aggregate (repository also handles outbox pattern for domain events)
        await _deliveryRepository.SaveAsync(delivery, cancellationToken);

        return new CompleteDeliveryResult
        {
            DeliveryId = delivery.Id,
            OrderId = delivery.OrderId,
            Status = delivery.Status.ToString(),
            DriverId = delivery.DriverId!.Value,
            DeliveredAt = delivery.DeliveredAt!.Value,
            TotalDeliveryMinutes = delivery.TotalDeliveryMinutes!.Value,
            SlaBreached = delivery.SlaBreached!.Value
        };
    }
}
