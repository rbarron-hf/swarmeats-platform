using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Orders.Domain.Events.Consumed;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Repositories;

namespace Orders.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for handling DeliveryCompleted events
/// from the Delivery context. Transitions the order status from InDelivery to Delivered.
/// This is the terminal happy-path transition for an order.
/// Invalid transitions are logged as warnings and the message is completed to prevent
/// poison message scenarios.
/// </summary>
public sealed class HandleDeliveryCompletedFunction
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<HandleDeliveryCompletedFunction> _logger;

    public HandleDeliveryCompletedFunction(IOrderRepository orderRepository, ILogger<HandleDeliveryCompletedFunction> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleDeliveryCompleted")]
    public async Task Run(
        [ServiceBusTrigger("delivery.completed", "orders-context", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("HandleDeliveryCompleted: Processing message {MessageId}.", message.MessageId);

        // 1. Deserialize the event from message body
        var deliveryCompletedEvent = JsonSerializer.Deserialize<DeliveryCompletedEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (deliveryCompletedEvent?.Payload is null)
        {
            _logger.LogWarning("HandleDeliveryCompleted: Failed to deserialize message {MessageId}. Completing to avoid poison message.", message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        var orderId = deliveryCompletedEvent.Payload.OrderId;
        _logger.LogInformation("HandleDeliveryCompleted: Processing DeliveryCompleted for orderId {OrderId}, eventId {EventId}, slaBreached {SlaBreached}.",
            orderId, deliveryCompletedEvent.EventId, deliveryCompletedEvent.Payload.SlaBreached);

        // 2. Load the Order aggregate from repository
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
        {
            _logger.LogWarning("HandleDeliveryCompleted: Order {OrderId} not found. Completing message to avoid poison message.", orderId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 3. Call the appropriate state transition method on the aggregate
        try
        {
            order.MarkDelivered();
        }
        catch (InvalidOrderTransitionException ex)
        {
            _logger.LogWarning(ex,
                "HandleDeliveryCompleted: Invalid transition for order {OrderId}. Current status: {CurrentStatus}. Discarding event.",
                orderId, ex.CurrentStatus);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 4. Save via repository (outbox pattern handles any raised events)
        await _orderRepository.SaveAsync(order);

        // 5. Complete the message
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation("HandleDeliveryCompleted: Successfully transitioned order {OrderId} to Delivered.", orderId);
    }
}
