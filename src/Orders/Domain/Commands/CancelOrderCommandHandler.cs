using MediatR;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Repositories;

namespace Orders.Domain.Commands;

/// <summary>
/// Handles the CancelOrderCommand by loading the order aggregate,
/// delegating to the aggregate's Cancel() method, and persisting the result.
/// No business logic lives here -- all rules are enforced inside the aggregate.
/// </summary>
public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResult>
{
    private readonly IOrderRepository _orderRepository;

    public CancelOrderCommandHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<CancelOrderResult> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        // Load the order aggregate from the repository
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
        {
            throw new OrderNotFoundException(request.OrderId);
        }

        // Delegate to the aggregate -- Cancel() enforces ORD-R05 internally
        order.Cancel();

        // Persist the updated aggregate (repository also handles outbox pattern for domain events)
        await _orderRepository.SaveAsync(order, cancellationToken);

        return new CancelOrderResult
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            CancelledAt = order.CancelledAt!.Value
        };
    }
}
