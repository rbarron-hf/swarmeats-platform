using MediatR;
using Restaurant.Domain.Aggregates;
using Restaurant.Infrastructure.Repositories;

namespace Restaurant.Domain.Queries;

/// <summary>
/// Handles the GetActiveOrdersQuery by loading restaurant orders from the repository
/// and mapping them to response DTOs. This is a pure read operation —
/// no business logic, no state changes, no domain events.
/// RST-002.
/// </summary>
public sealed class GetActiveOrdersQueryHandler : IRequestHandler<GetActiveOrdersQuery, GetActiveOrdersResponse>
{
    private readonly IRestaurantOrderRepository _orderRepository;

    public GetActiveOrdersQueryHandler(IRestaurantOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<GetActiveOrdersResponse> Handle(GetActiveOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetByRestaurantIdAsync(
            request.RestaurantId,
            request.Status,
            cancellationToken);

        return new GetActiveOrdersResponse
        {
            Orders = orders.Select(MapToSummary).ToList()
        };
    }

    private static GetActiveOrderSummaryResponse MapToSummary(RestaurantOrder order)
    {
        // Determine the most recent timestamp for UpdatedAt
        var updatedAt = order.CancelledAt
            ?? order.RejectedAt
            ?? order.ReadyAt
            ?? order.PreparingAt
            ?? order.AcceptedAt
            ?? order.ReceivedAt;

        return new GetActiveOrderSummaryResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            LineItems = order.LineItems.Select(li => new GetActiveOrderLineItemResponse
            {
                MenuItemId = li.MenuItemId,
                MenuItemName = li.MenuItemName,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice
            }).ToList(),
            CreatedAt = order.ReceivedAt,
            UpdatedAt = updatedAt
        };
    }
}
