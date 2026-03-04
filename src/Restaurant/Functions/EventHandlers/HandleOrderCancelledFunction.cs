using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Restaurant.Domain.Events.Consumed;
using Restaurant.Domain.ValueObjects;
using Restaurant.Infrastructure.Repositories;

namespace Restaurant.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for processing incoming OrderCancelled events.
/// Subscribes to orders.cancelled topic, restaurant-context subscription.
/// If the RestaurantOrder is in Pending status, marks it as Cancelled.
/// If already Accepted or later, logs a warning and does not change state.
/// If no RestaurantOrder exists, logs and discards the event.
/// RST-008.
/// </summary>
public sealed class HandleOrderCancelledFunction
{
    private readonly IRestaurantOrderRepository _orderRepository;
    private readonly ILogger<HandleOrderCancelledFunction> _logger;

    public HandleOrderCancelledFunction(
        IRestaurantOrderRepository orderRepository,
        ILogger<HandleOrderCancelledFunction> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleOrderCancelled")]
    public async Task Run(
        [ServiceBusTrigger("orders.cancelled", "restaurant-context", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("HandleOrderCancelled triggered. MessageId: {MessageId}", message.MessageId);

        OrderCancelledEvent? cancelledEvent;
        try
        {
            cancelledEvent = JsonSerializer.Deserialize<OrderCancelledEvent>(
                message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize OrderCancelled event. MessageId: {MessageId}", message.MessageId);
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Deserialization failure");
            return;
        }

        if (cancelledEvent?.Payload is null)
        {
            _logger.LogError("OrderCancelled event or payload is null. MessageId: {MessageId}", message.MessageId);
            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "Null payload");
            return;
        }

        var payload = cancelledEvent.Payload;

        _logger.LogInformation(
            "Processing OrderCancelled event. EventId: {EventId}, OrderId: {OrderId}, RestaurantId: {RestaurantId}",
            cancelledEvent.EventId, payload.OrderId, payload.RestaurantId);

        // --- Look up the RestaurantOrder ---
        var order = await _orderRepository.GetByIdAsync(payload.OrderId, payload.RestaurantId);

        if (order is null)
        {
            // Event arrived before OrderPlaced was processed, or order does not exist
            _logger.LogInformation(
                "No RestaurantOrder found for OrderId: {OrderId}, RestaurantId: {RestaurantId}. " +
                "OrderCancelled event arrived before OrderPlaced was processed. Discarding.",
                payload.OrderId, payload.RestaurantId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // --- Attempt to cancel the order ---
        var wasCancelled = order.Cancel();

        if (wasCancelled)
        {
            _logger.LogInformation(
                "Order {OrderId} cancelled successfully. Was in Pending status.",
                payload.OrderId);
            await _orderRepository.SaveAsync(order);
        }
        else
        {
            // Order is already Accepted or further along — log warning, do not change state
            _logger.LogWarning(
                "Cancellation too late for order {OrderId}. Current status: {Status}. " +
                "Food may already be in preparation. No state change applied.",
                payload.OrderId, order.Status);
        }

        await messageActions.CompleteMessageAsync(message);
    }
}
