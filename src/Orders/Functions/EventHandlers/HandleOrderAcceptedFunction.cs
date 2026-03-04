using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Orders.Domain.Events.Consumed;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Repositories;

namespace Orders.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for handling OrderAccepted events
/// from the Restaurant context. Transitions the order status from Placed to Accepted.
/// Invalid transitions are logged as warnings and the message is completed to prevent
/// poison message scenarios.
/// </summary>
public sealed class HandleOrderAcceptedFunction
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<HandleOrderAcceptedFunction> _logger;

    public HandleOrderAcceptedFunction(IOrderRepository orderRepository, ILogger<HandleOrderAcceptedFunction> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleOrderAccepted")]
    public async Task Run(
        [ServiceBusTrigger("restaurant.order-accepted", "orders-context", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("HandleOrderAccepted: Processing message {MessageId}.", message.MessageId);

        // 1. Deserialize the event from message body
        var orderAcceptedEvent = JsonSerializer.Deserialize<OrderAcceptedEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (orderAcceptedEvent?.Payload is null)
        {
            _logger.LogWarning("HandleOrderAccepted: Failed to deserialize message {MessageId}. Completing to avoid poison message.", message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        var orderId = orderAcceptedEvent.Payload.OrderId;
        _logger.LogInformation("HandleOrderAccepted: Processing OrderAccepted for orderId {OrderId}, eventId {EventId}.",
            orderId, orderAcceptedEvent.EventId);

        // 2. Load the Order aggregate from repository
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
        {
            _logger.LogWarning("HandleOrderAccepted: Order {OrderId} not found. Completing message to avoid poison message.", orderId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 3. Call the appropriate state transition method on the aggregate
        try
        {
            order.Accept();
        }
        catch (InvalidOrderTransitionException ex)
        {
            _logger.LogWarning(ex,
                "HandleOrderAccepted: Invalid transition for order {OrderId}. Current status: {CurrentStatus}. Discarding event.",
                orderId, ex.CurrentStatus);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 4. Save via repository (outbox pattern handles any raised events)
        await _orderRepository.SaveAsync(order);

        // 5. Complete the message
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation("HandleOrderAccepted: Successfully transitioned order {OrderId} to Accepted.", orderId);
    }
}
