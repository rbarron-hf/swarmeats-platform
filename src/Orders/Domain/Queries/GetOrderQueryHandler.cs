using MediatR;
using Orders.Domain.Aggregates;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Repositories;

namespace Orders.Domain.Queries;

/// <summary>
/// Handles the GetOrderQuery by loading the order aggregate from the repository
/// and mapping it to a GetOrderResponse DTO. This is a pure read operation —
/// no business logic, no state changes, no domain events.
/// </summary>
public sealed class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, GetOrderResponse>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<GetOrderResponse> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order is null)
        {
            throw new OrderNotFoundException(request.OrderId);
        }

        return MapToResponse(order);
    }

    private static GetOrderResponse MapToResponse(Order order)
    {
        return new GetOrderResponse
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            RestaurantId = order.RestaurantId,
            Status = order.Status.ToString(),
            LineItems = order.LineItems.Select(li => new GetOrderLineItemResponse
            {
                MenuItemId = li.MenuItemId,
                MenuItemName = li.MenuItemName,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice
            }).ToList(),
            DeliveryAddress = order.DeliveryAddress is not null
                ? new GetOrderDeliveryAddressResponse
                {
                    Street = order.DeliveryAddress.Street,
                    City = order.DeliveryAddress.City,
                    Postcode = order.DeliveryAddress.Postcode,
                    Latitude = order.DeliveryAddress.Latitude,
                    Longitude = order.DeliveryAddress.Longitude
                }
                : null,
            OrderTotal = order.OrderTotal is not null
                ? new GetOrderTotalResponse
                {
                    Subtotal = order.OrderTotal.Subtotal,
                    DeliveryFee = order.OrderTotal.DeliveryFee,
                    Total = order.OrderTotal.Total
                }
                : null,
            Timestamps = new GetOrderTimestampsResponse
            {
                CreatedAt = order.CreatedAt,
                PlacedAt = order.PlacedAt,
                AcceptedAt = order.AcceptedAt,
                PreparingAt = order.PreparingAt,
                ReadyForPickupAt = order.ReadyForPickupAt,
                InDeliveryAt = order.InDeliveryAt,
                DeliveredAt = order.DeliveredAt,
                RejectedAt = order.RejectedAt,
                CancelledAt = order.CancelledAt
            }
        };
    }
}
