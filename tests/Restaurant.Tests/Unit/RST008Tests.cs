using FluentAssertions;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.ValueObjects;
using Xunit;

namespace Restaurant.Tests.Unit;

/// <summary>
/// Unit tests for RST-008: Handle Order Cancelled.
/// Tests the RestaurantOrder aggregate Cancel() method and the cancellation logic
/// for orders in various statuses.
/// </summary>
public class RST008Tests
{
    #region Test Helpers

    private static RestaurantOrder CreateTestOrder(RestaurantOrderStatus status = RestaurantOrderStatus.Pending)
    {
        var order = new RestaurantOrder(
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            orderNumber: "ORD-20260303-001",
            lineItems: new List<RestaurantOrderLineItem>
            {
                new(Guid.NewGuid(), "Margherita Pizza", 2, 8.99m)
            },
            sourceEventId: Guid.NewGuid());

        if (status != RestaurantOrderStatus.Pending)
        {
            var statusProperty = typeof(RestaurantOrder).GetProperty(nameof(RestaurantOrder.Status));
            statusProperty!.SetValue(order, status);
        }

        return order;
    }

    #endregion

    /// <summary>
    /// AC: RST-008-AC-01
    /// When a pending order is cancelled, it transitions to Cancelled status.
    /// </summary>
    [Fact]
    public void Cancel_WhenPending_TransitionsToCancelled()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Pending);

        // Act
        var result = order.Cancel();

        // Assert
        result.Should().BeTrue();
        order.Status.Should().Be(RestaurantOrderStatus.Cancelled);
        order.CancelledAt.Should().NotBeNull();
        order.CancelledAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: RST-008-AC-02
    /// No domain events are raised when cancelling a RestaurantOrder.
    /// </summary>
    [Fact]
    public void Cancel_WhenPending_DoesNotRaiseDomainEvent()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Pending);

        // Act
        order.Cancel();

        // Assert
        order.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// AC: RST-008-ERR-02
    /// When an accepted order receives cancellation, it returns false (cancellation too late).
    /// </summary>
    [Fact]
    public void Cancel_WhenAccepted_ReturnsFalse()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Accepted);

        // Act
        var result = order.Cancel();

        // Assert
        result.Should().BeFalse();
        order.Status.Should().Be(RestaurantOrderStatus.Accepted);
        order.CancelledAt.Should().BeNull();
    }

    /// <summary>
    /// AC: RST-008-ERR-02
    /// When a preparing order receives cancellation, it returns false (cancellation too late).
    /// </summary>
    [Fact]
    public void Cancel_WhenPreparing_ReturnsFalse()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Preparing);

        // Act
        var result = order.Cancel();

        // Assert
        result.Should().BeFalse();
        order.Status.Should().Be(RestaurantOrderStatus.Preparing);
        order.CancelledAt.Should().BeNull();
    }

    /// <summary>
    /// AC: RST-008-ERR-02
    /// When a ReadyForPickup order receives cancellation, it returns false (cancellation too late).
    /// </summary>
    [Fact]
    public void Cancel_WhenReadyForPickup_ReturnsFalse()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.ReadyForPickup);

        // Act
        var result = order.Cancel();

        // Assert
        result.Should().BeFalse();
        order.Status.Should().Be(RestaurantOrderStatus.ReadyForPickup);
        order.CancelledAt.Should().BeNull();
    }

    /// <summary>
    /// AC: RST-008-AC-03
    /// When an already rejected order receives cancellation, it returns false.
    /// </summary>
    [Fact]
    public void Cancel_WhenAlreadyRejected_ReturnsFalse()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Rejected);

        // Act
        var result = order.Cancel();

        // Assert
        result.Should().BeFalse();
        order.Status.Should().Be(RestaurantOrderStatus.Rejected);
    }

    /// <summary>
    /// AC: RST-008-AC-04
    /// When an already cancelled order receives a duplicate cancellation, it returns false.
    /// </summary>
    [Fact]
    public void Cancel_WhenAlreadyCancelled_ReturnsFalse()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Cancelled);

        // Act
        var result = order.Cancel();

        // Assert
        result.Should().BeFalse();
        order.Status.Should().Be(RestaurantOrderStatus.Cancelled);
    }
}
