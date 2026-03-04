using FluentAssertions;
using Orders.Domain.Aggregates;
using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-007: Handle OrderReadyForPickup event.
/// Tests the Order aggregate MarkReadyForPickup() method including valid transitions
/// from both Accepted and Preparing statuses, invalid transitions, and state integrity.
/// </summary>
public class ORD007Tests
{
    #region Test Helpers

    /// <summary>
    /// Creates a test Order aggregate in the specified status.
    /// Uses reflection to set the Status property for testing non-Placed states.
    /// </summary>
    private static Order CreateTestOrder(OrderStatus status = OrderStatus.Accepted)
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
    /// AC: ORD-007-AC-01
    /// When an order is in Accepted status and MarkReadyForPickup() is called,
    /// the status transitions to ReadyForPickup.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenStatusIsAccepted_TransitionsToReadyForPickup()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Accepted);

        // Act
        order.MarkReadyForPickup();

        // Assert
        order.Status.Should().Be(OrderStatus.ReadyForPickup);
    }

    /// <summary>
    /// AC: ORD-007-AC-01
    /// When an order is in Preparing status and MarkReadyForPickup() is called,
    /// the status transitions to ReadyForPickup. This handles the case where
    /// the Order context may not have received an intermediate Preparing status update.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenStatusIsPreparing_TransitionsToReadyForPickup()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Preparing);

        // Act
        order.MarkReadyForPickup();

        // Assert
        order.Status.Should().Be(OrderStatus.ReadyForPickup);
    }

    /// <summary>
    /// AC: ORD-007-AC-02
    /// When an order is marked ready for pickup, the ReadyForPickupAt timestamp is recorded.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenValid_RecordsReadyForPickupAtTimestamp()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Accepted);

        // Act
        order.MarkReadyForPickup();

        // Assert
        order.ReadyForPickupAt.Should().NotBeNull();
        order.ReadyForPickupAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Invalid Transition Tests

    /// <summary>
    /// AC: ORD-007-ERR-01
    /// When an order is in Placed status, MarkReadyForPickup() throws
    /// InvalidOrderTransitionException. An order must be Accepted before it can be ready.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenStatusIsPlaced_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        var act = () => order.MarkReadyForPickup();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.OrderId == order.Id)
            .Where(ex => ex.CurrentStatus == OrderStatus.Placed)
            .Where(ex => ex.TargetStatus == OrderStatus.ReadyForPickup)
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_TRANSITION");

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Placed);
        order.ReadyForPickupAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-007-ERR-01
    /// When an order is in Cancelled status, MarkReadyForPickup() throws
    /// InvalidOrderTransitionException.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenStatusIsCancelled_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Cancelled);

        // Act
        var act = () => order.MarkReadyForPickup();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Cancelled)
            .Where(ex => ex.TargetStatus == OrderStatus.ReadyForPickup);

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.ReadyForPickupAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-007-ERR-01
    /// When an order is in Rejected status, MarkReadyForPickup() throws
    /// InvalidOrderTransitionException.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenStatusIsRejected_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Rejected);

        // Act
        var act = () => order.MarkReadyForPickup();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Rejected)
            .Where(ex => ex.TargetStatus == OrderStatus.ReadyForPickup);
    }

    #endregion
}
