using Restaurant.Domain.Events;
using Restaurant.Domain.Exceptions;
using Restaurant.Domain.ValueObjects;

namespace Restaurant.Domain.Aggregates;

/// <summary>
/// Aggregate root representing the Restaurant context's local representation of an incoming order.
/// Created when an OrderPlaced event is received from the Orders context.
/// Tracks the restaurant's processing of the order through its lifecycle.
/// Partition key in Cosmos DB restaurant-orders container: restaurantId.
/// </summary>
public class RestaurantOrder : AggregateRoot<Guid>
{
    /// <summary>
    /// The restaurant identifier. Also serves as the Cosmos DB partition key.
    /// </summary>
    public Guid RestaurantId { get; private set; }

    /// <summary>
    /// Human-readable order number from the Orders context.
    /// </summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>
    /// The line items in this order.
    /// </summary>
    public List<RestaurantOrderLineItem> LineItems { get; private set; } = new();

    /// <summary>
    /// Current status of the restaurant order within its lifecycle state machine.
    /// </summary>
    public RestaurantOrderStatus Status { get; private set; }

    /// <summary>
    /// Estimated preparation time in minutes. Set when the order is accepted (RST-R03: 5-90 min).
    /// </summary>
    public int? EstimatedPrepTime { get; private set; }

    /// <summary>
    /// Rejection reason code. Set when the order is rejected (RST-R04).
    /// </summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Optional notes provided with a rejection.
    /// </summary>
    public string? RejectionNotes { get; private set; }

    /// <summary>
    /// List of unavailable menu item IDs that caused auto-rejection.
    /// </summary>
    public List<Guid> UnavailableItemIds { get; private set; } = new();

    // --- State transition timestamps ---

    /// <summary>Timestamp when the order was received from the Orders context.</summary>
    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Timestamp when the restaurant accepted the order.</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>Timestamp when the restaurant began preparing the order.</summary>
    public DateTimeOffset? PreparingAt { get; private set; }

    /// <summary>Timestamp when the order was marked ready for pickup.</summary>
    public DateTimeOffset? ReadyAt { get; private set; }

    /// <summary>Timestamp when the order was rejected.</summary>
    public DateTimeOffset? RejectedAt { get; private set; }

    /// <summary>Timestamp when the order was cancelled (via external OrderCancelled event).</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// Event ID of the OrderPlaced event that created this order, used for idempotency.
    /// </summary>
    public Guid? SourceEventId { get; private set; }

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private RestaurantOrder() { }

    /// <summary>
    /// Creates a new RestaurantOrder aggregate from an incoming OrderPlaced event.
    /// The order starts in Pending status awaiting restaurant staff review.
    /// </summary>
    public RestaurantOrder(
        Guid orderId,
        Guid restaurantId,
        string orderNumber,
        List<RestaurantOrderLineItem> lineItems,
        Guid sourceEventId)
    {
        Id = orderId;
        RestaurantId = restaurantId;
        OrderNumber = orderNumber;
        LineItems = lineItems;
        Status = RestaurantOrderStatus.Pending;
        ReceivedAt = DateTimeOffset.UtcNow;
        SourceEventId = sourceEventId;
    }

    /// <summary>
    /// Accepts the order with an estimated preparation time.
    /// Transitions from Pending to Accepted. Raises OrderAccepted event.
    /// Enforces RST-R03 (prep time 5-90 min).
    /// </summary>
    /// <param name="estimatedPrepMinutes">Estimated preparation time in minutes (5-90).</param>
    /// <exception cref="ArgumentOutOfRangeException">When estimatedPrepMinutes is outside 5-90 range.</exception>
    /// <exception cref="InvalidOrderStateException">When order is not in Pending status.</exception>
    public void Accept(int estimatedPrepMinutes)
    {
        if (estimatedPrepMinutes < 5 || estimatedPrepMinutes > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(estimatedPrepMinutes),
                estimatedPrepMinutes,
                "Estimated preparation time must be between 5 and 90 minutes (RST-R03).");
        }

        if (Status != RestaurantOrderStatus.Pending)
        {
            throw new InvalidOrderStateException(Id, Status, RestaurantOrderStatus.Accepted, "RESTAURANT_ORDER_NOT_PENDING");
        }

        Status = RestaurantOrderStatus.Accepted;
        EstimatedPrepTime = estimatedPrepMinutes;
        AcceptedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderAccepted
        {
            OrderId = Id,
            RestaurantId = RestaurantId,
            EstimatedPrepMinutes = estimatedPrepMinutes,
            AcceptedAt = AcceptedAt.Value
        });
    }

    /// <summary>
    /// Rejects the order with a reason code and optional notes.
    /// Transitions from Pending to Rejected. Raises OrderRejected event.
    /// Enforces RST-R04 (valid reason codes) and RST-R05 (only from Pending).
    /// </summary>
    /// <param name="reasonCode">Rejection reason code from the allowed set.</param>
    /// <param name="notes">Optional notes from restaurant staff.</param>
    /// <param name="unavailableItemIds">Menu item IDs that were unavailable (for ITEM_UNAVAILABLE reason).</param>
    /// <exception cref="ArgumentException">When reasonCode is not in the allowed set.</exception>
    /// <exception cref="InvalidOrderStateException">When order is not in Pending status.</exception>
    public void Reject(string reasonCode, string? notes = null, List<Guid>? unavailableItemIds = null)
    {
        var allowedReasons = new[] { "RESTAURANT_CLOSED", "ITEM_UNAVAILABLE", "TOO_BUSY", "OTHER" };
        if (!allowedReasons.Contains(reasonCode))
        {
            throw new ArgumentException(
                $"Reason code '{reasonCode}' is not valid. Allowed values: {string.Join(", ", allowedReasons)}",
                nameof(reasonCode));
        }

        if (Status != RestaurantOrderStatus.Pending)
        {
            throw new InvalidOrderStateException(Id, Status, RestaurantOrderStatus.Rejected, "RESTAURANT_ORDER_NOT_PENDING");
        }

        Status = RestaurantOrderStatus.Rejected;
        RejectionReason = reasonCode;
        RejectionNotes = notes;
        UnavailableItemIds = unavailableItemIds ?? new List<Guid>();
        RejectedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderRejected
        {
            OrderId = Id,
            RestaurantId = RestaurantId,
            ReasonCode = reasonCode,
            UnavailableItemIds = UnavailableItemIds,
            RejectedAt = RejectedAt.Value
        });
    }

    /// <summary>
    /// Marks the order as being prepared.
    /// Transitions from Accepted to Preparing. No domain event raised.
    /// </summary>
    /// <exception cref="InvalidOrderStateException">When order is not in Accepted status.</exception>
    public void MarkPreparing()
    {
        if (Status != RestaurantOrderStatus.Accepted)
        {
            throw new InvalidOrderStateException(Id, Status, RestaurantOrderStatus.Preparing, "RESTAURANT_INVALID_TRANSITION");
        }

        Status = RestaurantOrderStatus.Preparing;
        PreparingAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the order as ready for driver pickup.
    /// Transitions from Preparing to ReadyForPickup. Raises OrderReadyForPickup event.
    /// </summary>
    /// <param name="restaurantAddress">The restaurant address for the pickup event payload.</param>
    /// <exception cref="InvalidOrderStateException">When order is not in Preparing status.</exception>
    public void MarkReadyForPickup(RestaurantAddress restaurantAddress)
    {
        if (Status != RestaurantOrderStatus.Preparing)
        {
            throw new InvalidOrderStateException(Id, Status, RestaurantOrderStatus.ReadyForPickup, "RESTAURANT_INVALID_TRANSITION");
        }

        Status = RestaurantOrderStatus.ReadyForPickup;
        ReadyAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new OrderReadyForPickup
        {
            OrderId = Id,
            RestaurantId = RestaurantId,
            RestaurantAddress = restaurantAddress,
            ReadyAt = ReadyAt.Value
        });
    }

    /// <summary>
    /// Cancels the order due to an external OrderCancelled event.
    /// Only cancels from Pending status; later statuses log a warning instead.
    /// </summary>
    /// <returns>True if the order was cancelled; false if the cancellation was too late.</returns>
    public bool Cancel()
    {
        if (Status != RestaurantOrderStatus.Pending)
        {
            return false;
        }

        Status = RestaurantOrderStatus.Cancelled;
        CancelledAt = DateTimeOffset.UtcNow;
        return true;
    }
}

/// <summary>
/// Value object representing one line item in a restaurant order.
/// </summary>
public record RestaurantOrderLineItem
{
    public Guid MenuItemId { get; init; }
    public string MenuItemName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }

    public RestaurantOrderLineItem(Guid menuItemId, string menuItemName, int quantity, decimal unitPrice)
    {
        MenuItemId = menuItemId;
        MenuItemName = menuItemName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>
    /// Parameterless constructor for deserialization (Cosmos DB SDK).
    /// </summary>
    private RestaurantOrderLineItem() { }
}
