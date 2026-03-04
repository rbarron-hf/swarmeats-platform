using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Delivery.Domain.Events.Consumed;
using Delivery.Domain.ValueObjects;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for handling OrderCancelled events
/// from the Orders context. Cancels deliveries in AwaitingDriver status or
/// logs a warning if a driver is already assigned.
/// DLV-009: Handle Order Cancelled for Delivery.
/// </summary>
public sealed class HandleOrderCancelledFunction
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<HandleOrderCancelledFunction> _logger;

    public HandleOrderCancelledFunction(
        IDeliveryRepository deliveryRepository,
        ILogger<HandleOrderCancelledFunction> logger)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleOrderCancelledDelivery")]
    public async Task Run(
        [ServiceBusTrigger("orders.cancelled", "delivery-context", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("OrderCancelled event received. MessageId: {MessageId}", message.MessageId);

        try
        {
            var eventEnvelope = JsonSerializer.Deserialize<OrderCancelledEvent>(
                message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (eventEnvelope is null || eventEnvelope.Payload is null)
            {
                _logger.LogWarning("Failed to deserialize OrderCancelled event. Completing message to avoid poison queue.");
                await messageActions.CompleteMessageAsync(message);
                return;
            }

            var payload = eventEnvelope.Payload;

            // Look up the delivery by orderId
            var delivery = await _deliveryRepository.GetByOrderIdAsync(payload.OrderId);

            if (delivery is null)
            {
                // No delivery exists for this order -- order may have been cancelled before food was ready
                _logger.LogInformation(
                    "No delivery found for cancelled orderId: {OrderId}. Event discarded silently.",
                    payload.OrderId);
                await messageActions.CompleteMessageAsync(message);
                return;
            }

            if (delivery.Status == DeliveryStatus.AwaitingDriver)
            {
                // Cancel the delivery
                delivery.Cancel();
                await _deliveryRepository.SaveAsync(delivery);

                _logger.LogInformation(
                    "Delivery cancelled. DeliveryId: {DeliveryId}, OrderId: {OrderId}",
                    delivery.Id, delivery.OrderId);
            }
            else if (delivery.Status == DeliveryStatus.DriverAssigned || delivery.Status == DeliveryStatus.PickedUp)
            {
                // Driver already assigned or has picked up -- log warning
                _logger.LogWarning(
                    "Order cancelled but delivery is already in progress. DeliveryId: {DeliveryId}, " +
                    "OrderId: {OrderId}, Status: {Status}. Driver should be notified out of band.",
                    delivery.Id, delivery.OrderId, delivery.Status);
            }
            else
            {
                _logger.LogInformation(
                    "Order cancelled but delivery is already in terminal state. DeliveryId: {DeliveryId}, " +
                    "OrderId: {OrderId}, Status: {Status}. No action taken.",
                    delivery.Id, delivery.OrderId, delivery.Status);
            }

            // Complete the Service Bus message
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderCancelled event. MessageId: {MessageId}", message.MessageId);
            throw; // Let the Service Bus retry policy handle the failure
        }
    }
}
