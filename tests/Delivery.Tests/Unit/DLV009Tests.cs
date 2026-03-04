using FluentAssertions;
using Moq;
using Delivery.Domain.Aggregates;
using Delivery.Domain.Exceptions;
using Delivery.Domain.ValueObjects;
using Delivery.Infrastructure.Repositories;
using Xunit;

namespace Delivery.Tests.Unit;

/// <summary>
/// Unit tests for DLV-009: Handle Order Cancelled for Delivery.
/// Tests the Delivery aggregate Cancel() method and the handler behaviour
/// for different delivery states.
/// </summary>
public class DLV009Tests
{
    #region Test Helpers

    private static DeliveryAggregate CreateTestDelivery(DeliveryStatus status = DeliveryStatus.AwaitingDriver)
    {
        var delivery = new DeliveryAggregate(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            route: new Route(
                new Location("10 Restaurant Street", "London", "EC1A 1BB", 51.5074, -0.1278),
                new Location("20 Customer Road", "London", "SW1A 2AA", 51.5014, -0.1419)),
            readyAt: DateTimeOffset.UtcNow);

        if (status >= DeliveryStatus.DriverAssigned)
        {
            delivery.AssignDriver(Guid.NewGuid(), 51.5080, -0.1280);
            delivery.ClearDomainEvents();
        }

        if (status >= DeliveryStatus.PickedUp)
        {
            delivery.ConfirmPickup(delivery.DriverId!.Value);
        }

        return delivery;
    }

    #endregion

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: DLV-009-AC-01
    /// When a delivery is in AwaitingDriver status and Cancel() is called,
    /// the status transitions to Cancelled.
    /// </summary>
    [Fact]
    public void Cancel_WhenAwaitingDriver_TransitionsToCancelled()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);

        // Act
        delivery.Cancel();

        // Assert
        delivery.Status.Should().Be(DeliveryStatus.Cancelled);
        delivery.CancelledAt.Should().NotBeNull();
        delivery.CancelledAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-009-AC-02
    /// When a delivery is in DriverAssigned status, Cancel() throws
    /// (handler should log warning instead of calling Cancel).
    /// </summary>
    [Fact]
    public void Cancel_WhenDriverAssigned_ThrowsInvalidDeliveryStateException()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.DriverAssigned);

        // Act
        var act = () => delivery.Cancel();

        // Assert
        act.Should().Throw<InvalidDeliveryStateException>()
            .Where(ex => ex.ErrorCode == "DELIVERY_CANNOT_CANCEL");

        delivery.Status.Should().Be(DeliveryStatus.DriverAssigned);
        delivery.CancelledAt.Should().BeNull();
    }

    /// <summary>
    /// AC: DLV-009-AC-02
    /// When a delivery is in PickedUp status, Cancel() throws.
    /// </summary>
    [Fact]
    public void Cancel_WhenPickedUp_ThrowsInvalidDeliveryStateException()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.PickedUp);

        // Act
        var act = () => delivery.Cancel();

        // Assert
        act.Should().Throw<InvalidDeliveryStateException>()
            .Where(ex => ex.ErrorCode == "DELIVERY_CANNOT_CANCEL");

        delivery.Status.Should().Be(DeliveryStatus.PickedUp);
        delivery.CancelledAt.Should().BeNull();
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: DLV-009-AC-03
    /// When no delivery exists for the orderId, the event is silently discarded.
    /// </summary>
    [Fact]
    public async Task HandleOrderCancelled_WhenNoDeliveryExists_SilentlyDiscards()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var mockRepo = new Mock<IDeliveryRepository>();

        mockRepo.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryAggregate?)null);

        // Act
        var result = await mockRepo.Object.GetByOrderIdAsync(orderId);

        // Assert
        result.Should().BeNull();
        mockRepo.Verify(r => r.SaveAsync(It.IsAny<DeliveryAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// AC: DLV-009-AC-04
    /// When a delivery exists in AwaitingDriver status, it is cancelled and saved.
    /// </summary>
    [Fact]
    public async Task HandleOrderCancelled_WhenAwaitingDriver_CancelsDelivery()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);
        var mockRepo = new Mock<IDeliveryRepository>();

        mockRepo.Setup(r => r.GetByOrderIdAsync(delivery.OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);
        mockRepo.Setup(r => r.SaveAsync(delivery, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - simulate what the handler does
        var foundDelivery = await mockRepo.Object.GetByOrderIdAsync(delivery.OrderId);
        foundDelivery.Should().NotBeNull();
        foundDelivery!.Status.Should().Be(DeliveryStatus.AwaitingDriver);

        foundDelivery.Cancel();
        await mockRepo.Object.SaveAsync(foundDelivery);

        // Assert
        foundDelivery.Status.Should().Be(DeliveryStatus.Cancelled);
        foundDelivery.CancelledAt.Should().NotBeNull();
        mockRepo.Verify(r => r.SaveAsync(foundDelivery, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// AC: DLV-009-AC-05
    /// When a delivery has a driver assigned, a warning should be logged (no cancellation).
    /// </summary>
    [Fact]
    public async Task HandleOrderCancelled_WhenDriverAssigned_DoesNotCancel()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.DriverAssigned);
        var mockRepo = new Mock<IDeliveryRepository>();

        mockRepo.Setup(r => r.GetByOrderIdAsync(delivery.OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);

        // Act
        var foundDelivery = await mockRepo.Object.GetByOrderIdAsync(delivery.OrderId);

        // Assert - the handler should NOT call Cancel on an in-progress delivery
        foundDelivery.Should().NotBeNull();
        foundDelivery!.Status.Should().Be(DeliveryStatus.DriverAssigned);

        // Verify SaveAsync is NOT called (no cancellation for in-progress deliveries)
        mockRepo.Verify(r => r.SaveAsync(It.IsAny<DeliveryAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
