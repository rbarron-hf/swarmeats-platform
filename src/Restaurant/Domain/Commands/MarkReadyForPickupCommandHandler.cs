using MediatR;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.Events;
using Restaurant.Domain.Exceptions;
using Restaurant.Infrastructure.Repositories;

namespace Restaurant.Domain.Commands;

/// <summary>
/// Handles the MarkReadyForPickupCommand by loading the RestaurantOrder aggregate,
/// loading the Menu for the restaurant address, delegating to the aggregate's
/// MarkReadyForPickup() method, and persisting the result.
/// No business logic lives here — all rules are enforced inside the aggregate.
/// RST-007.
/// </summary>
public sealed class MarkReadyForPickupCommandHandler : IRequestHandler<MarkReadyForPickupCommand, MarkReadyForPickupResult>
{
    private readonly IRestaurantOrderRepository _orderRepository;
    private readonly IMenuRepository _menuRepository;

    public MarkReadyForPickupCommandHandler(
        IRestaurantOrderRepository orderRepository,
        IMenuRepository menuRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _menuRepository = menuRepository ?? throw new ArgumentNullException(nameof(menuRepository));
    }

    public async Task<MarkReadyForPickupResult> Handle(MarkReadyForPickupCommand request, CancellationToken cancellationToken)
    {
        // Load the RestaurantOrder aggregate from the repository
        var order = await _orderRepository.GetByIdAsync(request.OrderId, request.RestaurantId, cancellationToken);

        if (order is null)
        {
            throw new RestaurantOrderNotFoundException(request.OrderId, request.RestaurantId);
        }

        // Load the menu to get the restaurant address for the event payload
        var menu = await _menuRepository.GetByRestaurantIdAsync(request.RestaurantId, cancellationToken);

        // Build the restaurant address for the event payload
        var restaurantAddress = new RestaurantAddress(
            Street: menu?.Address?.Street ?? string.Empty,
            City: menu?.Address?.City ?? string.Empty,
            Postcode: menu?.Address?.Postcode ?? string.Empty,
            Latitude: menu?.Address?.Latitude ?? 0,
            Longitude: menu?.Address?.Longitude ?? 0);

        // Delegate to the aggregate — MarkReadyForPickup() enforces state machine internally
        order.MarkReadyForPickup(restaurantAddress);

        // Persist the updated aggregate (repository also handles outbox pattern for domain events)
        await _orderRepository.SaveAsync(order, cancellationToken);

        return new MarkReadyForPickupResult
        {
            OrderId = order.Id,
            RestaurantId = order.RestaurantId,
            Status = order.Status.ToString(),
            ReadyAt = order.ReadyAt!.Value
        };
    }
}
