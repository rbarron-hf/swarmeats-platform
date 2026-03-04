using MediatR;
using Restaurant.Domain.Exceptions;
using Restaurant.Infrastructure.Repositories;

namespace Restaurant.Domain.Commands;

/// <summary>
/// Handles the RejectOrderCommand by loading the RestaurantOrder aggregate,
/// delegating to the aggregate's Reject() method, and persisting the result.
/// No business logic lives here — all rules are enforced inside the aggregate.
/// RST-005.
/// </summary>
public sealed class RejectOrderCommandHandler : IRequestHandler<RejectOrderCommand, RejectOrderResult>
{
    private readonly IRestaurantOrderRepository _orderRepository;

    public RejectOrderCommandHandler(IRestaurantOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<RejectOrderResult> Handle(RejectOrderCommand request, CancellationToken cancellationToken)
    {
        // Load the RestaurantOrder aggregate from the repository
        var order = await _orderRepository.GetByIdAsync(request.OrderId, request.RestaurantId, cancellationToken);

        if (order is null)
        {
            throw new RestaurantOrderNotFoundException(request.OrderId, request.RestaurantId);
        }

        // Delegate to the aggregate — Reject() enforces RST-R04 and RST-R05 internally
        order.Reject(request.ReasonCode, request.Notes);

        // Persist the updated aggregate (repository also handles outbox pattern for domain events)
        await _orderRepository.SaveAsync(order, cancellationToken);

        return new RejectOrderResult
        {
            OrderId = order.Id,
            RestaurantId = order.RestaurantId,
            Status = order.Status.ToString(),
            ReasonCode = order.RejectionReason!,
            Notes = order.RejectionNotes,
            RejectedAt = order.RejectedAt!.Value
        };
    }
}
