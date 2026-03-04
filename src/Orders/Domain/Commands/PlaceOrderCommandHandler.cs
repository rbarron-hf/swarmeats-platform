using MediatR;
using Orders.Domain.Aggregates;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Repositories;

namespace Orders.Domain.Commands;

/// <summary>
/// Handles the PlaceOrderCommand by validating business rules (ORD-R01 through ORD-R04),
/// creating the Order aggregate, and persisting it via the repository.
/// Validation logic lives here because it validates the request data before aggregate creation.
/// The aggregate constructor raises the OrderPlaced domain event.
/// </summary>
public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, PlaceOrderResult>
{
    private const decimal DeliveryFee = 2.99m;
    private const decimal MinimumSubtotal = 10.00m;
    private const int MaxLineItems = 20;

    private readonly IOrderRepository _orderRepository;

    public PlaceOrderCommandHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<PlaceOrderResult> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        // --- Validate request data (pre-aggregate-creation validation) ---
        ValidateRequest(request);

        // --- Build value objects ---
        var lineItems = request.LineItems.Select(li => new OrderLineItem(
            menuItemId: li.MenuItemId,
            menuItemName: li.MenuItemName,
            quantity: li.Quantity,
            unitPrice: li.UnitPrice
        )).ToList();

        var deliveryAddress = new DeliveryAddress(
            Street: request.DeliveryAddress.Street,
            City: request.DeliveryAddress.City,
            Postcode: request.DeliveryAddress.Postcode,
            Latitude: request.DeliveryAddress.Latitude!.Value,
            Longitude: request.DeliveryAddress.Longitude!.Value);

        var subtotal = lineItems.Sum(li => li.Quantity * li.UnitPrice);
        var orderTotal = new OrderTotal(subtotal, DeliveryFee, subtotal + DeliveryFee);

        // --- Generate order number (ORD-R07) ---
        var orderNumber = $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        // --- Create the aggregate (constructor raises OrderPlaced event) ---
        var order = new Order(
            id: Guid.NewGuid(),
            orderNumber: orderNumber,
            customerId: request.CustomerId,
            restaurantId: request.RestaurantId,
            lineItems: lineItems,
            deliveryAddress: deliveryAddress,
            orderTotal: orderTotal);

        // --- Persist the aggregate (repository handles outbox pattern for domain events) ---
        await _orderRepository.SaveAsync(order, cancellationToken);

        return new PlaceOrderResult
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            OrderTotal = order.OrderTotal!.Total
        };
    }

    /// <summary>
    /// Validates all PlaceOrder business rules before aggregate creation.
    /// </summary>
    private static void ValidateRequest(PlaceOrderCommand request)
    {
        // ORD-R04: customerId must be a non-empty Guid
        if (request.CustomerId == Guid.Empty)
        {
            throw new InvalidOrderException("ORDER_INVALID_CUSTOMER",
                "The customerId must be a valid, non-empty GUID.");
        }

        // ORD-R04: restaurantId must be a non-empty Guid
        if (request.RestaurantId == Guid.Empty)
        {
            throw new InvalidOrderException("ORDER_INVALID_RESTAURANT",
                "The restaurantId must be a valid, non-empty GUID.");
        }

        // ORD-R03: Delivery address must include latitude and longitude
        if (request.DeliveryAddress.Latitude is null || request.DeliveryAddress.Longitude is null)
        {
            throw new InvalidOrderException("ORDER_INVALID_ADDRESS",
                "Delivery address must include both latitude and longitude.");
        }

        // ORD-R02: Maximum 20 line items per order
        if (request.LineItems.Count > MaxLineItems)
        {
            throw new InvalidOrderException("ORDER_TOO_MANY_ITEMS",
                $"An order cannot contain more than {MaxLineItems} line items.");
        }

        // ORD-R03: Every line item must have quantity >= 1 and unitPrice > 0.00
        foreach (var lineItem in request.LineItems)
        {
            if (lineItem.Quantity < 1 || lineItem.UnitPrice <= 0)
            {
                throw new InvalidOrderException("ORDER_INVALID_LINE_ITEM",
                    $"Line item '{lineItem.MenuItemName}' has invalid quantity ({lineItem.Quantity}) or unit price ({lineItem.UnitPrice}). Quantity must be >= 1 and unit price must be > 0.00.");
            }
        }

        // ORD-R01: Minimum order subtotal is GBP 10.00 (excluding delivery fee)
        var subtotal = request.LineItems.Sum(li => li.Quantity * li.UnitPrice);
        if (subtotal < MinimumSubtotal)
        {
            throw new InvalidOrderException("ORDER_MINIMUM_NOT_MET",
                $"Order subtotal of {subtotal:F2} GBP is below the minimum of {MinimumSubtotal:F2} GBP.");
        }
    }
}
