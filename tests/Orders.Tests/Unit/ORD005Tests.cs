using FluentAssertions;
using Orders.Domain.Aggregates;
using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-005: Handle OrderAccepted event.
/// Tests the Order aggregate Accept() method including valid transitions,
/// invalid transitions, and idempotency considerations.
/// </summary>
public class ORD005Tests
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
    /// AC: ORD-005-AC-01
    /// When an order is in Placed status and Accept() is called,
    /// the status transitions to Accepted.
    /// </summary>
    [Fact]
    public void Accept_WhenStatusIsPlaced_TransitionsToAccepted()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        order.Accept();

        // Assert
        order.Status.Should().Be(OrderStatus.Accepted);
    }

    /// <summary>
    /// AC: ORD-005-AC-02
    /// When an order is accepted, the AcceptedAt timestamp is recorded.
    /// </summary>
    [Fact]
    public void Accept_WhenStatusIsPlaced_RecordsAcceptedAtTimestamp()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        order.Accept();

        // Assert
        order.AcceptedAt.Should().NotBeNull();
        order.AcceptedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Invalid Transition Tests

    /// <summary>
    /// AC: ORD-005-ERR-01
    /// When an order is in Cancelled status, Accept() throws InvalidOrderTransitionException.
    /// The event handler catches this and logs a warning without rethrowing.
    /// </summary>
    [Fact]
    public void Accept_WhenStatusIsCancelled_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Cancelled);

        // Act
        var act = () => order.Accept();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.OrderId == order.Id)
            .Where(ex => ex.CurrentStatus == OrderStatus.Cancelled)
            .Where(ex => ex.TargetStatus == OrderStatus.Accepted)
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_TRANSITION");

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.AcceptedAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-005-ERR-01
    /// When an order is in Rejected status, Accept() throws InvalidOrderTransitionException.
    /// </summary>
    [Fact]
    public void Accept_WhenStatusIsRejected_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Rejected);

        // Act
        var act = () => order.Accept();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Rejected)
            .Where(ex => ex.TargetStatus == OrderStatus.Accepted);

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Rejected);
        order.AcceptedAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-005-ERR-01
    /// When an order is already Accepted, calling Accept() again throws
    /// InvalidOrderTransitionException (idempotency handled at function level).
    /// </summary>
    [Fact]
    public void Accept_WhenStatusIsAlreadyAccepted_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Accepted);

        // Act
        var act = () => order.Accept();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Accepted)
            .Where(ex => ex.TargetStatus == OrderStatus.Accepted);
    }

    #endregion
}
