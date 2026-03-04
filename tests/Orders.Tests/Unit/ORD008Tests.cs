using FluentAssertions;
using Orders.Domain.Aggregates;
using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-008: Handle DriverAssigned event.
/// Tests the Order aggregate MarkInDelivery() method including valid transitions,
/// invalid transitions, and state integrity after failed transitions.
/// </summary>
public class ORD008Tests
{
    #region Test Helpers

    /// <summary>
    /// Creates a test Order aggregate in the specified status.
    /// Uses reflection to set the Status property for testing non-Placed states.
    /// </summary>
    private static Order CreateTestOrder(OrderStatus status = OrderStatus.ReadyForPickup)
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
    /// AC: ORD-008-AC-01
    /// When an order is in ReadyForPickup status and MarkInDelivery() is called,
    /// the status transitions to InDelivery.
    /// </summary>
    [Fact]
    public void MarkInDelivery_WhenStatusIsReadyForPickup_TransitionsToInDelivery()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.ReadyForPickup);

        // Act
        order.MarkInDelivery();

        // Assert
        order.Status.Should().Be(OrderStatus.InDelivery);
    }

    /// <summary>
    /// AC: ORD-008-AC-02
    /// When an order is marked in delivery, the InDeliveryAt timestamp is recorded.
    /// </summary>
    [Fact]
    public void MarkInDelivery_WhenValid_RecordsInDeliveryAtTimestamp()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.ReadyForPickup);

        // Act
        order.MarkInDelivery();

        // Assert
        order.InDeliveryAt.Should().NotBeNull();
        order.InDeliveryAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Invalid Transition Tests

    /// <summary>
    /// AC: ORD-008-ERR-01
    /// When an order is in Accepted status, MarkInDelivery() throws
    /// InvalidOrderTransitionException. An order must be ReadyForPickup first.
    /// </summary>
    [Fact]
    public void MarkInDelivery_WhenStatusIsAccepted_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Accepted);

        // Act
        var act = () => order.MarkInDelivery();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.OrderId == order.Id)
            .Where(ex => ex.CurrentStatus == OrderStatus.Accepted)
            .Where(ex => ex.TargetStatus == OrderStatus.InDelivery)
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_TRANSITION");

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Accepted);
        order.InDeliveryAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-008-ERR-01
    /// When an order is in Placed status, MarkInDelivery() throws
    /// InvalidOrderTransitionException.
    /// </summary>
    [Fact]
    public void MarkInDelivery_WhenStatusIsPlaced_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        var act = () => order.MarkInDelivery();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.Placed)
            .Where(ex => ex.TargetStatus == OrderStatus.InDelivery);

        // Verify aggregate state is completely unmodified
        order.Status.Should().Be(OrderStatus.Placed);
        order.InDeliveryAt.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-008-ERR-01
    /// When an order is already InDelivery, calling MarkInDelivery() again throws
    /// InvalidOrderTransitionException (idempotency handled at function level).
    /// </summary>
    [Fact]
    public void MarkInDelivery_WhenStatusIsAlreadyInDelivery_ThrowsInvalidOrderTransitionException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.InDelivery);

        // Act
        var act = () => order.MarkInDelivery();

        // Assert
        act.Should().Throw<InvalidOrderTransitionException>()
            .Where(ex => ex.CurrentStatus == OrderStatus.InDelivery)
            .Where(ex => ex.TargetStatus == OrderStatus.InDelivery);
    }

    #endregion
}
