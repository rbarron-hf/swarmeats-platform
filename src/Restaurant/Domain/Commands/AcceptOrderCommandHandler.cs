using MediatR;
using Restaurant.Domain.Exceptions;
using Restaurant.Infrastructure.Repositories;

namespace Restaurant.Domain.Commands;

/// <summary>
/// Handles the AcceptOrderCommand by loading the RestaurantOrder aggregate,
/// delegating to the aggregate's Accept() method, and persisting the result.
/// No business logic lives here — all rules are enforced inside the aggregate.
/// RST-004.
/// </summary>
public sealed class AcceptOrderCommandHandler : IRequestHandler<AcceptOrderCommand, AcceptOrderResult>
{
    private readonly IRestaurantOrderRepository _orderRepository;

    public AcceptOrderCommandHandler(IRestaurantOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<AcceptOrderResult> Handle(AcceptOrderCommand request, CancellationToken cancellationToken)
    {
        // Load the RestaurantOrder aggregate from the repository
        var order = await _orderRepository.GetByIdAsync(request.OrderId, request.RestaurantId, cancellationToken);

        if (order is null)
        {
            throw new RestaurantOrderNotFoundException(request.OrderId, request.RestaurantId);
        }

        // Delegate to the aggregate — Accept() enforces RST-R03 internally
        order.Accept(request.EstimatedPrepMinutes);

        // Persist the updated aggregate (repository also handles outbox pattern for domain events)
        await _orderRepository.SaveAsync(order, cancellationToken);

        return new AcceptOrderResult
        {
            OrderId = order.Id,
            RestaurantId = order.RestaurantId,
            Status = order.Status.ToString(),
            EstimatedPrepMinutes = order.EstimatedPrepTime!.Value,
            AcceptedAt = order.AcceptedAt!.Value
        };
    }
}
