using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Orders.Domain.Events.Consumed;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Repositories;

namespace Orders.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for handling OrderRejected events
/// from the Restaurant context. Transitions the order status from Placed to Rejected.
/// Invalid transitions are logged as warnings and the message is completed to prevent
/// poison message scenarios.
/// </summary>
public sealed class HandleOrderRejectedFunction
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<HandleOrderRejectedFunction> _logger;

    public HandleOrderRejectedFunction(IOrderRepository orderRepository, ILogger<HandleOrderRejectedFunction> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleOrderRejected")]
    public async Task Run(
        [ServiceBusTrigger("restaurant.order-rejected", "orders-context", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("HandleOrderRejected: Processing message {MessageId}.", message.MessageId);

        // 1. Deserialize the event from message body
        var orderRejectedEvent = JsonSerializer.Deserialize<OrderRejectedEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (orderRejectedEvent?.Payload is null)
        {
            _logger.LogWarning("HandleOrderRejected: Failed to deserialize message {MessageId}. Completing to avoid poison message.", message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        var orderId = orderRejectedEvent.Payload.OrderId;
        _logger.LogInformation("HandleOrderRejected: Processing OrderRejected for orderId {OrderId}, eventId {EventId}, reason {ReasonCode}.",
            orderId, orderRejectedEvent.EventId, orderRejectedEvent.Payload.ReasonCode);

        // 2. Load the Order aggregate from repository
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
        {
            _logger.LogWarning("HandleOrderRejected: Order {OrderId} not found. Completing message to avoid poison message.", orderId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 3. Call the appropriate state transition method on the aggregate
        try
        {
            order.Reject(orderRejectedEvent.Payload.ReasonCode);
        }
        catch (InvalidOrderTransitionException ex)
        {
            _logger.LogWarning(ex,
                "HandleOrderRejected: Invalid transition for order {OrderId}. Current status: {CurrentStatus}. Discarding event.",
                orderId, ex.CurrentStatus);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 4. Save via repository (outbox pattern handles any raised events)
        await _orderRepository.SaveAsync(order);

        // 5. Complete the message
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation("HandleOrderRejected: Successfully transitioned order {OrderId} to Rejected.", orderId);
    }
}
