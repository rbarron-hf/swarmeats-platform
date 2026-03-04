using MediatR;
using Orders.Domain.Aggregates;
using Orders.Infrastructure.Repositories;

namespace Orders.Domain.Queries;

/// <summary>
/// Handles the GetOrdersByCustomerQuery by loading orders from the repository
/// and mapping them to order summary DTOs. This is a pure read operation --
/// no business logic, no state changes, no domain events.
/// </summary>
public sealed class GetOrdersByCustomerQueryHandler : IRequestHandler<GetOrdersByCustomerQuery, GetOrdersByCustomerResponse>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrdersByCustomerQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<GetOrdersByCustomerResponse> Handle(GetOrdersByCustomerQuery request, CancellationToken cancellationToken)
    {
        var (orders, continuationToken) = await _orderRepository.GetByCustomerIdAsync(
            request.CustomerId,
            request.ContinuationToken,
            cancellationToken: cancellationToken);

        return new GetOrdersByCustomerResponse
        {
            Orders = orders.Select(MapToSummary).ToList(),
            ContinuationToken = continuationToken
        };
    }

    private static OrderSummaryResponse MapToSummary(Order order)
    {
        return new OrderSummaryResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            RestaurantName = string.Empty, // Restaurant name is not stored on the Order aggregate
            Total = order.OrderTotal?.Total ?? 0m,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt
        };
    }
}
