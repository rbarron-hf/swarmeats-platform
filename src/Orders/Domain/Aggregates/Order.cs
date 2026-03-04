using Orders.Domain.Events;
using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;

namespace Orders.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a customer's food order.
/// Owns the full order lifecycle from placement through final delivery confirmation.
/// </summary>
public class Order : AggregateRoot<Guid>
{
    /// <summary>
    /// Human-readable order number in format ORD-YYYYMMDD-sequential.
    /// </summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Identifier of the customer who placed the order.
    /// Also serves as the Cosmos DB partition key.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Identifier of the restaurant fulfilling the order.
    /// </summary>
    public Guid RestaurantId { get; private set; }

    /// <summary>
    /// Current status of the order within its lifecycle state machine.
    /// </summary>
    public OrderStatus Status { get; private set; }

    /// <summary>
    /// Line items in the order. Maximum 20 items per order (ORD-R02).
    /// </summary>
    public List<OrderLineItem> LineItems { get; private set; } = new();

    /// <summary>
    /// Delivery destination for the order.
    /// </summary>
    public DeliveryAddress? DeliveryAddress { get; private set; }

    /// <summary>
    /// Financial summary: subtotal, delivery fee, and total.
    /// </summary>
    public OrderTotal? OrderTotal { get; private set; }

    // --- State transition timestamps ---

    /// <summary>Timestamp when the order was created (Draft).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Timestamp when the customer placed the order.</summary>
    public DateTimeOffset? PlacedAt { get; private set; }

    /// <summary>Timestamp when the restaurant accepted the order.</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>Timestamp when the restaurant began preparing the order.</summary>
    public DateTimeOffset? PreparingAt { get; private set; }

    /// <summary>Timestamp when the order was ready for driver pickup.</summary>
    public DateTimeOffset? ReadyForPickupAt { get; private set; }

    /// <summary>Timestamp when a driver picked up the order for delivery.</summary>
    public DateTimeOffset? InDeliveryAt { get; private set; }

    /// <summary>Timestamp when the order was delivered to the customer.</summary>
    public DateTimeOffset? DeliveredAt { get; private set; }

    /// <summary>Timestamp when the restaurant rejected the order.</summary>
    public DateTimeOffset? RejectedAt { get; private set; }

    /// <summary>Timestamp when the customer cancelled the order.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private Order() { }

    /// <summary>
    /// Creates a new Order aggregate. Used by the PlaceOrder flow (ORD-001).
    /// Raises an OrderPlaced domain event upon creation.
    /// </summary>
    public Order(
        Guid id,
        string orderNumber,
        Guid customerId,
        Guid restaurantId,
        List<OrderLineItem> lineItems,
        DeliveryAddress deliveryAddress,
        OrderTotal orderTotal)
    {
        Id = id;
        OrderNumber = orderNumber;
        CustomerId = customerId;
        RestaurantId = restaurantId;
        Status = OrderStatus.Placed;
        LineItems = lineItems;
        DeliveryAddress = deliveryAddress;
        OrderTotal = orderTotal;
        CreatedAt = DateTimeOffset.UtcNow;
        PlacedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderPlaced
        {
            OrderId = Id,
            OrderNumber = OrderNumber,
            CustomerId = CustomerId,
            RestaurantId = RestaurantId,
            LineItems = LineItems.Select(li => new OrderPlacedLineItem
            {
                MenuItemId = li.MenuItemId,
                MenuItemName = li.MenuItemName,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice
            }).ToList(),
            DeliveryAddress = new OrderPlacedDeliveryAddress
            {
                Street = DeliveryAddress.Street,
                City = DeliveryAddress.City,
                Postcode = DeliveryAddress.Postcode,
                Latitude = DeliveryAddress.Latitude,
                Longitude = DeliveryAddress.Longitude
            },
            OrderTotal = OrderTotal.Total,
            PlacedAt = PlacedAt!.Value
        });
    }

    /// <summary>
    /// Accepts the order. Transitions status from Placed to Accepted.
    /// Called when an OrderAccepted event is received from the Restaurant context (ORD-005).
    /// </summary>
    /// <exception cref="InvalidOrderTransitionException">
    /// Thrown when the order status is not Placed.
    /// </exception>
    public void Accept()
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOrderTransitionException(Id, Status, OrderStatus.Accepted);
        }

        Status = OrderStatus.Accepted;
        AcceptedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Rejects the order. Transitions status from Placed to Rejected.
    /// Called when an OrderRejected event is received from the Restaurant context (ORD-006).
    /// </summary>
    /// <param name="reason">The reason code for rejection.</param>
    /// <exception cref="InvalidOrderTransitionException">
    /// Thrown when the order status is not Placed.
    /// </exception>
    public void Reject(string reason)
    {
        if (Status != OrderStatus.Placed)
        {
            throw new InvalidOrderTransitionException(Id, Status, OrderStatus.Rejected);
        }

        Status = OrderStatus.Rejected;
        RejectedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the order as ready for pickup. Transitions status from Accepted or Preparing to ReadyForPickup.
    /// Called when an OrderReadyForPickup event is received from the Restaurant context (ORD-007).
    /// Accepts transitions from both Accepted and Preparing since the Order context may not
    /// have received an intermediate Preparing status update.
    /// </summary>
    /// <exception cref="InvalidOrderTransitionException">
    /// Thrown when the order status is not Accepted or Preparing.
    /// </exception>
    public void MarkReadyForPickup()
    {
        if (Status != OrderStatus.Accepted && Status != OrderStatus.Preparing)
        {
            throw new InvalidOrderTransitionException(Id, Status, OrderStatus.ReadyForPickup);
        }

        Status = OrderStatus.ReadyForPickup;
        ReadyForPickupAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the order as in delivery. Transitions status from ReadyForPickup to InDelivery.
    /// Called when a DriverAssigned event is received from the Delivery context (ORD-008).
    /// </summary>
    /// <exception cref="InvalidOrderTransitionException">
    /// Thrown when the order status is not ReadyForPickup.
    /// </exception>
    public void MarkInDelivery()
    {
        if (Status != OrderStatus.ReadyForPickup)
        {
            throw new InvalidOrderTransitionException(Id, Status, OrderStatus.InDelivery);
        }

        Status = OrderStatus.InDelivery;
        InDeliveryAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the order as delivered. Transitions status from InDelivery to Delivered.
    /// Called when a DeliveryCompleted event is received from the Delivery context (ORD-009).
    /// This is the terminal happy-path transition for an order.
    /// </summary>
    /// <exception cref="InvalidOrderTransitionException">
    /// Thrown when the order status is not InDelivery.
    /// </exception>
    public void MarkDelivered()
    {
        if (Status != OrderStatus.InDelivery)
        {
            throw new InvalidOrderTransitionException(Id, Status, OrderStatus.Delivered);
        }

        Status = OrderStatus.Delivered;
        DeliveredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Cancels the order. Only permitted while status is Placed (business rule ORD-R05).
    /// Transitions status to Cancelled, records the cancellation timestamp,
    /// and raises an OrderCancelled domain event.
    /// </summary>
    /// <exception cref="OrderCannotBeCancelledException">
    /// Thrown when the order status is not Placed.
    /// </exception>
    public void Cancel()
    {
        if (Status != OrderStatus.Placed)
        {
            throw new OrderCannotBeCancelledException(Id, Status);
        }

        Status = OrderStatus.Cancelled;
        CancelledAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderCancelled
        {
            OrderId = Id,
            OrderNumber = OrderNumber,
            RestaurantId = RestaurantId,
            CancelledAt = CancelledAt.Value
        });
    }
}

/// <summary>
/// Value object representing one item in an order. References a menu item ID,
/// menu item name, quantity (must be >= 1), and unit price (must be > 0.00).
/// Immutable by design — all properties are init-only via the primary constructor.
/// </summary>
public record OrderLineItem
{
    public Guid MenuItemId { get; init; }
    public string MenuItemName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }

    public OrderLineItem(Guid menuItemId, string menuItemName, int quantity, decimal unitPrice)
    {
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be at least 1.");
        if (unitPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice, "Unit price must be greater than zero.");

        MenuItemId = menuItemId;
        MenuItemName = menuItemName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private OrderLineItem() { }
}

/// <summary>
/// Value object for the customer's delivery location.
/// Contains street, city, postcode, latitude, and longitude.
/// </summary>
public record DeliveryAddress(
    string Street,
    string City,
    string Postcode,
    double Latitude,
    double Longitude);

/// <summary>
/// Value object for the order's financial summary:
/// subtotal, delivery fee (fixed 2.99 GBP), and total.
/// </summary>
public record OrderTotal(
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total);
