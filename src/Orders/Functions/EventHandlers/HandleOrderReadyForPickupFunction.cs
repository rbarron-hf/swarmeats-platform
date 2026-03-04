using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Orders.Domain.Events.Consumed;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Repositories;

namespace Orders.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for handling OrderReadyForPickup events
/// from the Restaurant context. Transitions the order status to ReadyForPickup.
/// Accepts transitions from both Accepted and Preparing statuses since the Order context
/// may not have received an intermediate Preparing status update.
/// Invalid transitions are logged as warnings and the message is completed.
/// </summary>
public sealed class HandleOrderReadyForPickupFunction
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<HandleOrderReadyForPickupFunction> _logger;

    public HandleOrderReadyForPickupFunction(IOrderRepository orderRepository, ILogger<HandleOrderReadyForPickupFunction> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleOrderReady")]
    public async Task Run(
        [ServiceBusTrigger("restaurant.order-ready", "orders-context", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("HandleOrderReady: Processing message {MessageId}.", message.MessageId);

        // 1. Deserialize the event from message body
        var readyEvent = JsonSerializer.Deserialize<OrderReadyForPickupEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (readyEvent?.Payload is null)
        {
            _logger.LogWarning("HandleOrderReady: Failed to deserialize message {MessageId}. Completing to avoid poison message.", message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        var orderId = readyEvent.Payload.OrderId;
        _logger.LogInformation("HandleOrderReady: Processing OrderReadyForPickup for orderId {OrderId}, eventId {EventId}.",
            orderId, readyEvent.EventId);

        // 2. Load the Order aggregate from repository
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
        {
            _logger.LogWarning("HandleOrderReady: Order {OrderId} not found. Completing message to avoid poison message.", orderId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 3. Call the appropriate state transition method on the aggregate
        try
        {
            order.MarkReadyForPickup();
        }
        catch (InvalidOrderTransitionException ex)
        {
            _logger.LogWarning(ex,
                "HandleOrderReady: Invalid transition for order {OrderId}. Current status: {CurrentStatus}. Discarding event.",
                orderId, ex.CurrentStatus);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 4. Save via repository (outbox pattern handles any raised events)
        await _orderRepository.SaveAsync(order);

        // 5. Complete the message
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation("HandleOrderReady: Successfully transitioned order {OrderId} to ReadyForPickup.", orderId);
    }
}
