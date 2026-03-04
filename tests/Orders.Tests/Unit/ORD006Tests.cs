using FluentAssertions;
using Orders.Domain.Aggregates;
using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-006: Handle OrderRejected event.
/// Tests the Order aggregate Reject() method including valid transitions,
/// invalid transitions, and state integrity after failed transitions.
/// </summary>
public class ORD006Tests
{
    #region Test Helpers

    /// <summary>
    /// Creates a test Order aggregate in the specified status.
    /// Uses reflection to set the Status property for testing non-Placed states.
    /// </summary>
    private static Order CreateTestOrder(OrderStatus status = OrderStatus.Placed)
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
    /// AC: ORD-006-AC-01
    /// When an order is in Placed status and Reject() is called,
    /// the status transitions to Rejected.
    /// </summary>
    [Fact]
    public void Reject_WhenStatusIsPlaced_TransitionsToRejected()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        order.Reject("ITEM_UNAVAILABLE");

        // Assert
        order.Status.Should().Be(OrderStatus.Rejected);
    }

    /// <summary>
    /// AC: ORD-006-AC-02
    /// When an order is rejected, the RejectedAt timestamp is recorded.
    /// </summary>
    [Fact]
    public void Reject_WhenStatusIsPlaced_RecordsRejectedAtTimestamp()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        order.Reject("TOO_BUSY");

        // Assert
        order.RejectedAt.Should().NotBeNull();
        order.RejectedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Invalid Transition Tests

    /// <summary>
    /// AC: ORD-006-ERR-01
    /// When an order is in Cancelled status, Reject() throws InvalidOrderTransitionException.
    /// </summary>
    [Fact]
    public void Reject_WhenStatusIsCancelled_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Cancelled);

        // Act
        var act = () => order.Reject("TOO_BUSY");

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.OrderId == order.Id)
            .Where(ex => ex.CurrentStatus == OrderStatus.Cancelled)
            .Where(ex => ex.TargetStatus == OrderStatus.Rejected)
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_TRANSITION");

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.RejectedAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-006-ERR-01
    /// When an order is in Accepted status, Reject() throws InvalidOrderTransitionException.
    /// Once accepted, an order cannot be rejected.
    /// </summary>
    [Fact]
    public void Reject_WhenStatusIsAccepted_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Accepted);

        // Act
        var act = () => order.Reject("TOO_BUSY");

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Accepted)
            .Where(ex => ex.TargetStatus == OrderStatus.Rejected);

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Accepted);
        order.RejectedAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-006-ERR-01
    /// When an order is already Rejected, calling Reject() again throws
    /// InvalidOrderTransitionException (idempotency handled at function level).
    /// </summary>
    [Fact]
    public void Reject_WhenStatusIsAlreadyRejected_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Rejected);

        // Act
        var act = () => order.Reject("OTHER");

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Rejected)
            .Where(ex => ex.TargetStatus == OrderStatus.Rejected);
    }

    #endregion
}
