using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.Events.Consumed;
using Restaurant.Infrastructure.Repositories;

namespace Restaurant.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for processing incoming OrderPlaced events.
/// Subscribes to orders.placed topic, restaurant-context subscription.
/// Creates a RestaurantOrder in Pending status after validating operating hours (RST-R01)
/// and menu item availability (RST-R02). Auto-rejects if validation fails.
/// RST-003 — the most complex handler in the Restaurant context.
/// </summary>
public sealed class HandleOrderPlacedFunction
{
    private readonly IMenuRepository _menuRepository;
    private readonly IRestaurantOrderRepository _orderRepository;
    private readonly ILogger<HandleOrderPlacedFunction> _logger;

    public HandleOrderPlacedFunction(
        IMenuRepository menuRepository,
        IRestaurantOrderRepository orderRepository,
        ILogger<HandleOrderPlacedFunction> logger)
    {
        _menuRepository = menuRepository ?? throw new ArgumentNullException(nameof(menuRepository));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleOrderPlaced")]
    public async Task Run(
        [ServiceBusTrigger("orders.placed", "restaurant-context", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("HandleOrderPlaced triggered. MessageId: {MessageId}", message.MessageId);

        OrderPlacedEvent? orderPlacedEvent;
        try
        {
            orderPlacedEvent = JsonSerializer.Deserialize<OrderPlacedEvent>(
                message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize OrderPlaced event. MessageId: {MessageId}", message.MessageId);
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Deserialization failure");
            return;
        }

        if (orderPlacedEvent?.Payload is null)
        {
            _logger.LogError("OrderPlaced event or payload is null. MessageId: {MessageId}", message.MessageId);
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Null payload");
            return;
        }

        var payload = orderPlacedEvent.Payload;

        _logger.LogInformation(
            "Processing OrderPlaced event. EventId: {EventId}, OrderId: {OrderId}, RestaurantId: {RestaurantId}",
            orderPlacedEvent.EventId, payload.OrderId, payload.RestaurantId);

        // --- Idempotency check: has this order already been processed? ---
        var alreadyExists = await _orderRepository.ExistsAsync(payload.OrderId, payload.RestaurantId);
        if (alreadyExists)
        {
            _logger.LogInformation(
                "Duplicate event detected. OrderId: {OrderId} already exists for restaurant: {RestaurantId}. Completing message.",
                payload.OrderId, payload.RestaurantId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // --- Build line items ---
        var lineItems = payload.LineItems.Select(li =>
            new RestaurantOrderLineItem(
                li.MenuItemId,
                li.MenuItemName,
                li.Quantity,
                li.UnitPrice)).ToList();

        // --- Create the RestaurantOrder aggregate ---
        var restaurantOrder = new RestaurantOrder(
            orderId: payload.OrderId,
            restaurantId: payload.RestaurantId,
            orderNumber: payload.OrderNumber,
            lineItems: lineItems,
            sourceEventId: orderPlacedEvent.EventId);

        // --- Load the restaurant menu for validation ---
        var menu = await _menuRepository.GetByRestaurantIdAsync(payload.RestaurantId);

        if (menu is null)
        {
            _logger.LogWarning(
                "Menu not found for restaurant: {RestaurantId}. Auto-rejecting order: {OrderId}",
                payload.RestaurantId, payload.OrderId);

            restaurantOrder.Reject("RESTAURANT_CLOSED", "Menu not found for restaurant");
            await _orderRepository.SaveAsync(restaurantOrder);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // --- RST-R01: Validate operating hours ---
        if (!menu.IsOpen(DateTimeOffset.UtcNow))
        {
            _logger.LogInformation(
                "Restaurant {RestaurantId} is outside operating hours. Auto-rejecting order: {OrderId}",
                payload.RestaurantId, payload.OrderId);

            restaurantOrder.Reject("RESTAURANT_CLOSED", "Restaurant is outside operating hours");
            await _orderRepository.SaveAsync(restaurantOrder);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // --- RST-R02: Validate menu item availability ---
        var requestedMenuItemIds = payload.LineItems.Select(li => li.MenuItemId).ToList();
        var unavailableItemIds = menu.GetUnavailableItemIds(requestedMenuItemIds);

        if (unavailableItemIds.Any())
        {
            _logger.LogInformation(
                "Unavailable menu items detected for order: {OrderId}. Items: {UnavailableItems}",
                payload.OrderId, string.Join(", ", unavailableItemIds));

            restaurantOrder.Reject("ITEM_UNAVAILABLE", "One or more menu items are unavailable", unavailableItemIds);
            await _orderRepository.SaveAsync(restaurantOrder);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // --- All validations passed: save as Pending ---
        _logger.LogInformation(
            "Order {OrderId} passed all validations. Saving as Pending for restaurant: {RestaurantId}",
            payload.OrderId, payload.RestaurantId);

        await _orderRepository.SaveAsync(restaurantOrder);
        await messageActions.CompleteMessageAsync(message);
    }
}
