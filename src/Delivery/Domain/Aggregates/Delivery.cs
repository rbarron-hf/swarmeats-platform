using Delivery.Domain.Events;
using Delivery.Domain.Exceptions;
using Delivery.Domain.ValueObjects;

namespace Delivery.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a food delivery from restaurant to customer.
/// Owns the full delivery lifecycle from creation (AwaitingDriver) through driver
/// assignment, pickup, and final delivery completion.
/// </summary>
public class DeliveryAggregate : AggregateRoot<Guid>
{
    /// <summary>
    /// Cross-reference to the order in the Orders context.
    /// </summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Identifier of the restaurant where food is picked up.
    /// </summary>
    public Guid RestaurantId { get; private set; }

    /// <summary>
    /// Identifier of the assigned driver. Null until a driver is assigned.
    /// Also serves as the Cosmos DB partition key (sentinel value "unassigned" when null).
    /// </summary>
    public Guid? DriverId { get; private set; }

    /// <summary>
    /// Current status of the delivery within its lifecycle state machine.
    /// </summary>
    public DeliveryStatus Status { get; private set; }

    /// <summary>
    /// The delivery route containing pickup (restaurant) and dropoff (customer) addresses.
    /// </summary>
    public Route? Route { get; private set; }

    /// <summary>
    /// The driver's last known geographic position. Null until a driver is assigned.
    /// </summary>
    public DriverLocation? DriverLocation { get; private set; }

    /// <summary>
    /// Estimated arrival time at the customer's address. Null until a driver is assigned.
    /// Updated on each driver location ping.
    /// </summary>
    public EstimatedArrival? EstimatedArrival { get; private set; }

    /// <summary>
    /// Timestamp from the OrderReadyForPickup event. Used for SLA tracking (DLV-R03).
    /// </summary>
    public DateTimeOffset ReadyAt { get; private set; }

    // --- State transition timestamps ---

    /// <summary>Timestamp when the delivery was created (AwaitingDriver).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Timestamp when a driver was assigned.</summary>
    public DateTimeOffset? DriverAssignedAt { get; private set; }

    /// <summary>Timestamp when the driver confirmed pickup.</summary>
    public DateTimeOffset? PickedUpAt { get; private set; }

    /// <summary>Timestamp when the delivery was completed.</summary>
    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>Timestamp when the delivery was cancelled.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// Total delivery time in minutes from readyAt to deliveredAt.
    /// Populated on delivery completion.
    /// </summary>
    public int? TotalDeliveryMinutes { get; private set; }

    /// <summary>
    /// Whether the 45-minute SLA was breached. Populated on delivery completion.
    /// </summary>
    public bool? SlaBreached { get; private set; }

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private DeliveryAggregate() { }

    /// <summary>
    /// Creates a new Delivery aggregate in AwaitingDriver status.
    /// Used by the HandleOrderReadyForPickup event handler (DLV-001).
    /// </summary>
    /// <param name="id">Unique delivery identifier.</param>
    /// <param name="orderId">Cross-reference to the order.</param>
    /// <param name="restaurantId">Restaurant identifier.</param>
    /// <param name="route">Delivery route with pickup and dropoff locations.</param>
    /// <param name="readyAt">Timestamp from the OrderReadyForPickup event for SLA tracking.</param>
    public DeliveryAggregate(
        Guid id,
        Guid orderId,
        Guid restaurantId,
        Route route,
        DateTimeOffset readyAt)
    {
        Id = id;
        OrderId = orderId;
        RestaurantId = restaurantId;
        Status = DeliveryStatus.AwaitingDriver;
        Route = route;
        ReadyAt = readyAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Assigns a driver to this delivery. Transitions from AwaitingDriver to DriverAssigned.
    /// Validates DLV-R02 (driver within 5km of restaurant via Haversine).
    /// Calculates initial estimated arrival. Raises DriverAssigned domain event.
    /// </summary>
    /// <param name="driverId">The driver being assigned.</param>
    /// <param name="driverLatitude">Driver's current latitude.</param>
    /// <param name="driverLongitude">Driver's current longitude.</param>
    /// <exception cref="InvalidDeliveryStateException">
    /// Thrown when the delivery is not in AwaitingDriver status.
    /// </exception>
    /// <exception cref="DriverTooFarException">
    /// Thrown when the driver is more than 5km from the restaurant.
    /// </exception>
    public void AssignDriver(Guid driverId, double driverLatitude, double driverLongitude)
    {
        if (Status != DeliveryStatus.AwaitingDriver)
        {
            throw new InvalidDeliveryStateException(
                Id, Status, "DELIVERY_ALREADY_ASSIGNED",
                $"Delivery '{Id}' cannot be assigned a driver. Current status is '{Status}'. Assignment is only permitted when status is 'AwaitingDriver'.");
        }

        // DLV-R02: Validate driver is within 5km of restaurant
        var distanceKm = GeoCalculations.CalculateDistanceKm(
            driverLatitude, driverLongitude,
            Route!.Pickup.Latitude, Route.Pickup.Longitude);

        if (distanceKm > GeoCalculations.MaxAssignmentDistanceKm)
        {
            throw new DriverTooFarException(driverId, distanceKm);
        }

        // Perform the state transition
        DriverId = driverId;
        Status = DeliveryStatus.DriverAssigned;
        DriverAssignedAt = DateTimeOffset.UtcNow;

        // Set initial driver location
        DriverLocation = new DriverLocation(driverLatitude, driverLongitude, DateTimeOffset.UtcNow);

        // Calculate initial estimated arrival (driver -> restaurant + restaurant -> customer)
        var driverToRestaurantMinutes = GeoCalculations.EstimateTravelMinutes(
            driverLatitude, driverLongitude,
            Route.Pickup.Latitude, Route.Pickup.Longitude);

        var restaurantToCustomerMinutes = GeoCalculations.EstimateTravelMinutes(
            Route.Pickup.Latitude, Route.Pickup.Longitude,
            Route.Dropoff.Latitude, Route.Dropoff.Longitude);

        var totalMinutes = driverToRestaurantMinutes + restaurantToCustomerMinutes;
        EstimatedArrival = new EstimatedArrival(
            DateTimeOffset.UtcNow.AddMinutes(totalMinutes),
            totalMinutes);

        // Raise the DriverAssigned domain event (DLV-R04)
        AddDomainEvent(new Events.DriverAssigned
        {
            DeliveryId = Id,
            OrderId = OrderId,
            DriverId = driverId,
            EstimatedArrivalMinutes = totalMinutes,
            AssignedAt = DriverAssignedAt.Value
        });
    }

    /// <summary>
    /// Updates the driver's current location and recalculates the estimated arrival time.
    /// Only permitted when delivery is in DriverAssigned or PickedUp status.
    /// Validates that the requesting driver matches the assigned driver.
    /// </summary>
    /// <param name="driverId">The driver reporting their location.</param>
    /// <param name="latitude">Driver's current latitude.</param>
    /// <param name="longitude">Driver's current longitude.</param>
    /// <exception cref="InvalidDeliveryStateException">
    /// Thrown when the delivery is not in DriverAssigned or PickedUp status.
    /// </exception>
    /// <exception cref="WrongDriverException">
    /// Thrown when the requesting driver does not match the assigned driver.
    /// </exception>
    public void UpdateDriverLocation(Guid driverId, double latitude, double longitude)
    {
        if (Status != DeliveryStatus.DriverAssigned && Status != DeliveryStatus.PickedUp)
        {
            throw new InvalidDeliveryStateException(
                Id, Status, "DELIVERY_NOT_ACTIVE",
                $"Delivery '{Id}' does not accept location updates. Current status is '{Status}'. Updates are only accepted in 'DriverAssigned' or 'PickedUp' status.");
        }

        if (DriverId != driverId)
        {
            throw new WrongDriverException(Id, driverId);
        }

        // Update driver location
        DriverLocation = new DriverLocation(latitude, longitude, DateTimeOffset.UtcNow);

        // Recalculate estimated arrival based on distance to dropoff
        var minutesToDropoff = GeoCalculations.EstimateTravelMinutes(
            latitude, longitude,
            Route!.Dropoff.Latitude, Route.Dropoff.Longitude);

        EstimatedArrival = new EstimatedArrival(
            DateTimeOffset.UtcNow.AddMinutes(minutesToDropoff),
            minutesToDropoff);
    }

    /// <summary>
    /// Confirms that the driver has picked up the food from the restaurant.
    /// Transitions from DriverAssigned to PickedUp.
    /// Validates that the requesting driver matches the assigned driver.
    /// </summary>
    /// <param name="driverId">The driver confirming pickup.</param>
    /// <exception cref="InvalidDeliveryStateException">
    /// Thrown when the delivery is not in DriverAssigned status.
    /// </exception>
    /// <exception cref="WrongDriverException">
    /// Thrown when the requesting driver does not match the assigned driver.
    /// </exception>
    public void ConfirmPickup(Guid driverId)
    {
        if (Status != DeliveryStatus.DriverAssigned)
        {
            throw new InvalidDeliveryStateException(
                Id, Status, "DELIVERY_INVALID_TRANSITION",
                $"Delivery '{Id}' cannot confirm pickup. Current status is '{Status}'. Pickup confirmation is only permitted in 'DriverAssigned' status.");
        }

        if (DriverId != driverId)
        {
            throw new WrongDriverException(Id, driverId);
        }

        Status = DeliveryStatus.PickedUp;
        PickedUpAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Completes the delivery to the customer. Transitions from PickedUp to Delivered.
    /// Calculates total delivery time and SLA breach status (DLV-R03).
    /// Raises DeliveryCompleted domain event.
    /// Validates that the requesting driver matches the assigned driver.
    /// </summary>
    /// <param name="driverId">The driver confirming delivery completion.</param>
    /// <exception cref="InvalidDeliveryStateException">
    /// Thrown when the delivery is not in PickedUp status.
    /// </exception>
    /// <exception cref="WrongDriverException">
    /// Thrown when the requesting driver does not match the assigned driver.
    /// </exception>
    public void Complete(Guid driverId)
    {
        if (Status != DeliveryStatus.PickedUp)
        {
            throw new InvalidDeliveryStateException(
                Id, Status, "DELIVERY_NOT_PICKED_UP",
                $"Delivery '{Id}' cannot be completed. Current status is '{Status}'. Completion is only permitted in 'PickedUp' status.");
        }

        if (DriverId != driverId)
        {
            throw new WrongDriverException(Id, driverId);
        }

        Status = DeliveryStatus.Delivered;
        DeliveredAt = DateTimeOffset.UtcNow;

        // Calculate SLA metrics (DLV-R03)
        TotalDeliveryMinutes = (int)(DeliveredAt.Value - ReadyAt).TotalMinutes;
        SlaBreached = TotalDeliveryMinutes > GeoCalculations.SlaThresholdMinutes;

        // Raise the DeliveryCompleted domain event
        AddDomainEvent(new Events.DeliveryCompleted
        {
            DeliveryId = Id,
            OrderId = OrderId,
            DriverId = driverId,
            DeliveredAt = DeliveredAt.Value,
            TotalDeliveryMinutes = TotalDeliveryMinutes.Value,
            SlaBreached = SlaBreached.Value
        });
    }

    /// <summary>
    /// Cancels the delivery. Only permitted in AwaitingDriver status.
    /// Used when an OrderCancelled event is received (DLV-009).
    /// </summary>
    /// <exception cref="InvalidDeliveryStateException">
    /// Thrown when the delivery is not in a cancellable state.
    /// </exception>
    public void Cancel()
    {
        if (Status != DeliveryStatus.AwaitingDriver)
        {
            throw new InvalidDeliveryStateException(
                Id, Status, "DELIVERY_CANNOT_CANCEL",
                $"Delivery '{Id}' cannot be cancelled. Current status is '{Status}'. Cancellation from the Delivery context is only permitted in 'AwaitingDriver' status.");
        }

        Status = DeliveryStatus.Cancelled;
        CancelledAt = DateTimeOffset.UtcNow;
    }
}
