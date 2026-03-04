using FluentAssertions;
using Moq;
using Delivery.Domain.Aggregates;
using Delivery.Domain.Commands;
using Delivery.Domain.Exceptions;
using Delivery.Domain.ValueObjects;
using Delivery.Infrastructure.Repositories;
using Xunit;

namespace Delivery.Tests.Unit;

/// <summary>
/// Unit tests for DLV-004: Confirm Pickup.
/// Tests the Delivery aggregate ConfirmPickup() method and the handler.
/// </summary>
public class DLV004Tests
{
    #region Test Helpers

    private static DeliveryAggregate CreateAssignedDelivery(Guid? driverId = null)
    {
        var delivery = new DeliveryAggregate(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            route: new Route(
                new Location("10 Restaurant Street", "London", "EC1A 1BB", 51.5074, -0.1278),
                new Location("20 Customer Road", "London", "SW1A 2AA", 51.5014, -0.1419)),
            readyAt: DateTimeOffset.UtcNow);

        var assignedDriverId = driverId ?? Guid.NewGuid();
        delivery.AssignDriver(assignedDriverId, 51.5080, -0.1280);
        delivery.ClearDomainEvents();

        return delivery;
    }

    #endregion

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: DLV-004-AC-01
    /// When confirmed by the assigned driver, status transitions from DriverAssigned to PickedUp.
    /// </summary>
    [Fact]
    public void ConfirmPickup_WhenDriverAssigned_TransitionsToPickedUp()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreateAssignedDelivery(driverId);

        // Act
        delivery.ConfirmPickup(driverId);

        // Assert
        delivery.Status.Should().Be(DeliveryStatus.PickedUp);
        delivery.PickedUpAt.Should().NotBeNull();
        delivery.PickedUpAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-004-ERR-02
    /// When the delivery is not in DriverAssigned status, ConfirmPickup throws.
    /// </summary>
    [Fact]
    public void ConfirmPickup_WhenAwaitingDriver_ThrowsInvalidDeliveryStateException()
    {
        // Arrange
        var delivery = new DeliveryAggregate(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            route: new Route(
                new Location("10 Restaurant Street", "London", "EC1A 1BB", 51.5074, -0.1278),
                new Location("20 Customer Road", "London", "SW1A 2AA", 51.5014, -0.1419)),
            readyAt: DateTimeOffset.UtcNow);

        // Act
        var act = () => delivery.ConfirmPickup(Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidDeliveryStateException>()
            .Where(ex => ex.ErrorCode == "DELIVERY_INVALID_TRANSITION");
    }

    /// <summary>
    /// AC: DLV-004-ERR-03
    /// When the driver ID does not match, ConfirmPickup throws WrongDriverException.
    /// </summary>
    [Fact]
    public void ConfirmPickup_WrongDriver_ThrowsWrongDriverException()
    {
        // Arrange
        var assignedDriverId = Guid.NewGuid();
        var wrongDriverId = Guid.NewGuid();
        var delivery = CreateAssignedDelivery(assignedDriverId);

        // Act
        var act = () => delivery.ConfirmPickup(wrongDriverId);

        // Assert
        act.Should().Throw<WrongDriverException>()
            .Where(ex => ex.DeliveryId == delivery.Id)
            .Where(ex => ex.RequestedDriverId == wrongDriverId)
            .Where(ex => ex.ErrorCode == "DELIVERY_WRONG_DRIVER");

        delivery.Status.Should().Be(DeliveryStatus.DriverAssigned);
        delivery.PickedUpAt.Should().BeNull();
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: DLV-004-ERR-01
    /// When the delivery does not exist, the handler throws DeliveryNotFoundException.
    /// </summary>
    [Fact]
    public async Task ConfirmPickup_WhenDeliveryNotFound_ThrowsDeliveryNotFoundException()
    {
        // Arrange
        var mockRepo = new Mock<IDeliveryRepository>();
        var deliveryId = Guid.NewGuid();

        mockRepo
            .Setup(r => r.GetByIdAsync(deliveryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryAggregate?)null);

        var handler = new ConfirmPickupCommandHandler(mockRepo.Object);
        var command = new ConfirmPickupCommand
        {
            DeliveryId = deliveryId,
            DriverId = Guid.NewGuid()
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DeliveryNotFoundException>()
            .Where(ex => ex.DeliveryId == deliveryId);
    }

    /// <summary>
    /// On successful pickup, the handler returns the updated delivery with PickedUp status.
    /// </summary>
    [Fact]
    public async Task ConfirmPickup_ReturnsUpdatedDeliveryWithPickedUpStatus()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreateAssignedDelivery(driverId);

        var mockRepo = new Mock<IDeliveryRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);
        mockRepo.Setup(r => r.SaveAsync(delivery, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ConfirmPickupCommandHandler(mockRepo.Object);
        var command = new ConfirmPickupCommand
        {
            DeliveryId = delivery.Id,
            DriverId = driverId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryId.Should().Be(delivery.Id);
        result.Status.Should().Be("PickedUp");
        result.DriverId.Should().Be(driverId);
        result.PickedUpAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        mockRepo.Verify(r => r.SaveAsync(delivery, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
