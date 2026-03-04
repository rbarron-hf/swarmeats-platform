using MediatR;
using Restaurant.Domain.Exceptions;
using Restaurant.Infrastructure.Repositories;

namespace Restaurant.Domain.Commands;

/// <summary>
/// Handles the MarkPreparingCommand by loading the RestaurantOrder aggregate,
/// delegating to the aggregate's MarkPreparing() method, and persisting the result.
/// No business logic lives here — all rules are enforced inside the aggregate.
/// RST-006.
/// </summary>
public sealed class MarkPreparingCommandHandler : IRequestHandler<MarkPreparingCommand, MarkPreparingResult>
{
    private readonly IRestaurantOrderRepository _orderRepository;

    public MarkPreparingCommandHandler(IRestaurantOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<MarkPreparingResult> Handle(MarkPreparingCommand request, CancellationToken cancellationToken)
    {
        // Load the RestaurantOrder aggregate from the repository
        var order = await _orderRepository.GetByIdAsync(request.OrderId, request.RestaurantId, cancellationToken);

        if (order is null)
        {
            throw new RestaurantOrderNotFoundException(request.OrderId, request.RestaurantId);
        }

        // Delegate to the aggregate — MarkPreparing() enforces state machine internally
        order.MarkPreparing();

        // Persist the updated aggregate (no domain events for this transition)
        await _orderRepository.SaveAsync(order, cancellationToken);

        return new MarkPreparingResult
        {
            OrderId = order.Id,
            RestaurantId = order.RestaurantId,
            Status = order.Status.ToString(),
            UpdatedAt = order.PreparingAt!.Value
        };
    }
}
