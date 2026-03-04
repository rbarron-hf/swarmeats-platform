using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Delivery.Domain.Aggregates;
using Delivery.Domain.Events.Consumed;
using Delivery.Domain.ValueObjects;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Functions.EventHandlers;

/// <summary>
/// Azure Function Service Bus trigger for handling OrderReadyForPickup events
/// from the Restaurant context. Creates a new Delivery aggregate in AwaitingDriver status.
/// DLV-001: Handle OrderReadyForPickup Event.
/// </summary>
public sealed class HandleOrderReadyForPickupFunction
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<HandleOrderReadyForPickupFunction> _logger;

    public HandleOrderReadyForPickupFunction(
        IDeliveryRepository deliveryRepository,
        ILogger<HandleOrderReadyForPickupFunction> logger)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("HandleOrderReadyForPickup")]
    public async Task Run(
        [ServiceBusTrigger("restaurant.order-ready", "delivery-subscription", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("OrderReadyForPickup event received. MessageId: {MessageId}", message.MessageId);

        try
        {
            var eventEnvelope = JsonSerializer.Deserialize<OrderReadyForPickupEvent>(
                message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (eventEnvelope is null || eventEnvelope.Payload is null)
            {
                _logger.LogWarning("Failed to deserialize OrderReadyForPickup event. Completing message to avoid poison queue.");
                await messageActions.CompleteMessageAsync(message);
                return;
            }

            // Idempotency check: see if a delivery already exists for this orderId
            var existingDelivery = await _deliveryRepository.GetByOrderIdAsync(eventEnvelope.Payload.OrderId);
            if (existingDelivery is not null)
            {
                _logger.LogInformation(
                    "Delivery already exists for orderId: {OrderId}. Discarding duplicate event. EventId: {EventId}",
                    eventEnvelope.Payload.OrderId, eventEnvelope.EventId);
                await messageActions.CompleteMessageAsync(message);
                return;
            }

            var payload = eventEnvelope.Payload;

            // Map event addresses to domain value objects
            var pickupLocation = new Location(
                payload.RestaurantAddress.Street,
                payload.RestaurantAddress.City,
                payload.RestaurantAddress.Postcode,
                payload.RestaurantAddress.Latitude,
                payload.RestaurantAddress.Longitude);

            var dropoffLocation = new Location(
                payload.DeliveryAddress.Street,
                payload.DeliveryAddress.City,
                payload.DeliveryAddress.Postcode,
                payload.DeliveryAddress.Latitude,
                payload.DeliveryAddress.Longitude);

            var route = new Route(pickupLocation, dropoffLocation);

            // Create the new Delivery aggregate
            var delivery = new DeliveryAggregate(
                id: Guid.NewGuid(),
                orderId: payload.OrderId,
                restaurantId: payload.RestaurantId,
                route: route,
                readyAt: payload.ReadyAt);

            // Persist to Cosmos DB with 'unassigned' partition key
            await _deliveryRepository.SaveAsync(delivery);

            _logger.LogInformation(
                "Delivery created. DeliveryId: {DeliveryId}, OrderId: {OrderId}, Status: {Status}",
                delivery.Id, delivery.OrderId, delivery.Status);

            // Complete the Service Bus message
            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OrderReadyForPickup event. MessageId: {MessageId}", message.MessageId);
            throw; // Let the Service Bus retry policy handle the failure
        }
    }
}
