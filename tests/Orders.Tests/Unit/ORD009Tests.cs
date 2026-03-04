using FluentAssertions;
using Orders.Domain.Aggregates;
using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-009: Handle DeliveryCompleted event.
/// Tests the Order aggregate MarkDelivered() method including valid transitions,
/// invalid transitions, and state integrity after failed transitions.
/// This is the terminal happy-path transition for an order.
/// </summary>
public class ORD009Tests
{
    #region Test Helpers

    /// <summary>
    /// Creates a test Order aggregate in the specified status.
    /// Uses reflection to set the Status property for testing non-Placed states.
    /// </summary>
    private static Order CreateTestOrder(OrderStatus status = OrderStatus.InDelivery)
    {
        var order = new Order(
            id: Guid.NewGuid(),
            orderNumber: "ORD-20260303-001",
            customerId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            lineItems: new List<OrderLineItem>
            {
                new(
                    menuItemId: Guid.NewGuid(),
                    menuItemName: "Margherita Pizza",
                    quantity: 2,
                    unitPrice: 8.99m)
            },
            deliveryAddress: new DeliveryAddress("123 High Street", "London", "SW1A 1AA", 51.5074, -0.1278),
            orderTotal: new OrderTotal(17.98m, 2.99m, 20.97m));

        if (status != OrderStatus.Placed)
        {
            var statusProperty = typeof(Order).GetProperty(nameof(Order.Status));
            statusProperty!.SetValue(order, status);
        }

        return order;
    }

    #endregion

    #region Valid Transition Tests

    /// <summary>
    /// AC: ORD-009-AC-01
    /// When an order is in InDelivery status and MarkDelivered() is called,
    /// the status transitions to Delivered.
    /// </summary>
    [Fact]
    public void MarkDelivered_WhenStatusIsInDelivery_TransitionsToDelivered()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.InDelivery);

        // Act
        order.MarkDelivered();

        // Assert
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    /// <summary>
    /// AC: ORD-009-AC-02
    /// When an order is marked delivered, the DeliveredAt timestamp is recorded.
    /// </summary>
    [Fact]
    public void MarkDelivered_WhenValid_RecordsDeliveredAtTimestamp()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.InDelivery);

        // Act
        order.MarkDelivered();

        // Assert
        order.DeliveredAt.Should().NotBeNull();
        order.DeliveredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Invalid Transition Tests

    /// <summary>
    /// AC: ORD-009-ERR-01
    /// When an order is in ReadyForPickup status, MarkDelivered() throws
    /// InvalidOrderTransitionException. An order must be InDelivery first.
    /// </summary>
    [Fact]
    public void MarkDelivered_WhenStatusIsReadyForPickup_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.ReadyForPickup);

        // Act
        var act = () => order.MarkDelivered();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.OrderId == order.Id)
            .Where(ex => ex.CurrentStatus == OrderStatus.ReadyForPickup)
            .Where(ex => ex.TargetStatus == OrderStatus.Delivered)
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_TRANSITION");

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.ReadyForPickup);
        order.DeliveredAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-009-ERR-01
    /// When an order is in Placed status, MarkDelivered() throws
    /// InvalidOrderTransitionException.
    /// </summary>
    [Fact]
    public void MarkDelivered_WhenStatusIsPlaced_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        var act = () => order.MarkDelivered();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Placed)
            .Where(ex => ex.TargetStatus == OrderStatus.Delivered);

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Placed);
        order.DeliveredAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-009-ERR-01
    /// When an order is already Delivered, calling MarkDelivered() again throws
    /// InvalidOrderTransitionException (idempotency handled at function level).
    /// </summary>
    [Fact]
    public void MarkDelivered_WhenStatusIsAlreadyDelivered_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Delivered);

        // Act
        var act = () => order.MarkDelivered();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Delivered)
            .Where(ex => ex.TargetStatus == OrderStatus.Delivered);
    }

    /// <summary>
    /// AC: ORD-009-ERR-01
    /// When an order is in Cancelled status, MarkDelivered() throws
    /// InvalidOrderTransitionException.
    /// </summary>
    [Fact]
    public void MarkDelivered_WhenStatusIsCancelled_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Cancelled);

        // Act
        var act = () => order.MarkDelivered();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Cancelled)
            .Where(ex => ex.TargetStatus == OrderStatus.Delivered);

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DeliveredAt.Should().BeNull();
    }

    #endregion
}
