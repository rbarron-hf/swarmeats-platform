using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Orders.Domain.Events.Consumed;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Repositories;

namespace Orders.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for handling DriverAssigned events
/// from the Delivery context. Transitions the order status from ReadyForPickup to InDelivery.
/// Invalid transitions are logged as warnings and the message is completed to prevent
/// poison message scenarios.
/// </summary>
public sealed class HandleDriverAssignedFunction
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<HandleDriverAssignedFunction> _logger;

    public HandleDriverAssignedFunction(IOrderRepository orderRepository, ILogger<HandleDriverAssignedFunction> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleDriverAssigned")]
    public async Task Run(
        [ServiceBusTrigger("delivery.driver-assigned", "orders-context", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("HandleDriverAssigned: Processing message {MessageId}.", message.MessageId);

        // 1. Deserialize the event from message body
        var driverAssignedEvent = JsonSerializer.Deserialize<DriverAssignedEvent>(
            message.Body.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (driverAssignedEvent?.Payload is null)
        {
            _logger.LogWarning("HandleDriverAssigned: Failed to deserialize message {MessageId}. Completing to avoid poison message.", message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        var orderId = driverAssignedEvent.Payload.OrderId;
        _logger.LogInformation("HandleDriverAssigned: Processing DriverAssigned for orderId {OrderId}, driverId {DriverId}, eventId {EventId}.",
            orderId, driverAssignedEvent.Payload.DriverId, driverAssignedEvent.EventId);

        // 2. Load the Order aggregate from repository
        var order = await _orderRepository.GetByIdAsync(orderId);

        if (order is null)
        {
            _logger.LogWarning("HandleDriverAssigned: Order {OrderId} not found. Completing message to avoid poison message.", orderId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 3. Call the appropriate state transition method on the aggregate
        try
        {
            order.MarkInDelivery();
        }
        catch (InvalidOrderTransitionException ex)
        {
            _logger.LogWarning(ex,
                "HandleDriverAssigned: Invalid transition for order {OrderId}. Current status: {CurrentStatus}. Discarding event.",
                orderId, ex.CurrentStatus);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // 4. Save via repository (outbox pattern handles any raised events)
        await _orderRepository.SaveAsync(order);

        // 5. Complete the message
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation("HandleDriverAssigned: Successfully transitioned order {OrderId} to InDelivery.", orderId);
    }
}
