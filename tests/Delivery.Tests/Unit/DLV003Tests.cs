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
/// Unit tests for DLV-003: Update Driver Location.
/// Tests the Delivery aggregate UpdateDriverLocation() method and the handler.
/// </summary>
public class DLV003Tests
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
        delivery.ClearDomainEvents(); // Clear events from assignment

        return delivery;
    }

    #endregion

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: DLV-003-AC-01
    /// When a valid location update is received, the DriverLocation is updated.
    /// </summary>
    [Fact]
    public void UpdateDriverLocation_WhenDriverAssigned_UpdatesLocation()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreateAssignedDelivery(driverId);
        var newLat = 51.5060;
        var newLon = -0.1350;

        // Act
        delivery.UpdateDriverLocation(driverId, newLat, newLon);

        // Assert
        delivery.DriverLocation.Should().NotBeNull();
        delivery.DriverLocation!.Latitude.Should().Be(newLat);
        delivery.DriverLocation!.Longitude.Should().Be(newLon);
        delivery.DriverLocation!.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-003-AC-02
    /// The EstimatedArrival is recalculated based on the new driver location.
    /// </summary>
    [Fact]
    public void UpdateDriverLocation_RecalculatesEstimatedArrival()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreateAssignedDelivery(driverId);
        var previousEta = delivery.EstimatedArrival;

        // Act - move driver closer to dropoff
        delivery.UpdateDriverLocation(driverId, 51.5014, -0.1419);

        // Assert
        delivery.EstimatedArrival.Should().NotBeNull();
        // New ETA should be different (closer to dropoff should mean shorter time)
        delivery.EstimatedArrival!.EstimatedMinutes.Should().BeLessThanOrEqualTo(previousEta!.EstimatedMinutes);
    }

    /// <summary>
    /// AC: DLV-003-ERR-02
    /// When the delivery is not active (e.g., AwaitingDriver), location update throws InvalidDeliveryStateException.
    /// </summary>
    [Fact]
    public void UpdateDriverLocation_WhenAwaitingDriver_ThrowsInvalidDeliveryStateException()
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
        var act = () => delivery.UpdateDriverLocation(Guid.NewGuid(), 51.5060, -0.1350);

        // Assert
        act.Should().Throw<InvalidDeliveryStateException>()
            .Where(ex => ex.ErrorCode == "DELIVERY_NOT_ACTIVE");
    }

    /// <summary>
    /// AC: DLV-003-ERR-03
    /// When the driver ID does not match the assigned driver, throws WrongDriverException.
    /// </summary>
    [Fact]
    public void UpdateDriverLocation_WrongDriver_ThrowsWrongDriverException()
    {
        // Arrange
        var assignedDriverId = Guid.NewGuid();
        var wrongDriverId = Guid.NewGuid();
        var delivery = CreateAssignedDelivery(assignedDriverId);

        // Act
        var act = () => delivery.UpdateDriverLocation(wrongDriverId, 51.5060, -0.1350);

        // Assert
        act.Should().Throw<WrongDriverException>()
            .Where(ex => ex.DeliveryId == delivery.Id)
            .Where(ex => ex.RequestedDriverId == wrongDriverId)
            .Where(ex => ex.ErrorCode == "DELIVERY_WRONG_DRIVER");
    }

    /// <summary>
    /// Location updates should be accepted in PickedUp status too.
    /// </summary>
    [Fact]
    public void UpdateDriverLocation_WhenPickedUp_UpdatesLocation()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreateAssignedDelivery(driverId);
        delivery.ConfirmPickup(driverId);

        // Act
        delivery.UpdateDriverLocation(driverId, 51.5030, -0.1300);

        // Assert
        delivery.DriverLocation!.Latitude.Should().Be(51.5030);
        delivery.DriverLocation!.Longitude.Should().Be(-0.1300);
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: DLV-003-ERR-01
    /// When the delivery does not exist, the handler throws DeliveryNotFoundException.
    /// </summary>
    [Fact]
    public async Task UpdateDriverLocation_WhenDeliveryNotFound_ThrowsDeliveryNotFoundException()
    {
        // Arrange
        var mockRepo = new Mock<IDeliveryRepository>();
        var deliveryId = Guid.NewGuid();

        mockRepo
            .Setup(r => r.GetByIdAsync(deliveryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryAggregate?)null);

        var handler = new UpdateDriverLocationCommandHandler(mockRepo.Object);
        var command = new UpdateDriverLocationCommand
        {
            DeliveryId = deliveryId,
            DriverId = Guid.NewGuid(),
            Latitude = 51.5060,
            Longitude = -0.1350
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DeliveryNotFoundException>()
            .Where(ex => ex.DeliveryId == deliveryId);
    }

    /// <summary>
    /// On successful update, the handler returns the updated estimated arrival.
    /// </summary>
    [Fact]
    public async Task UpdateDriverLocation_ReturnsUpdatedEstimatedArrival()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreateAssignedDelivery(driverId);

        var mockRepo = new Mock<IDeliveryRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);
        mockRepo.Setup(r => r.SaveAsync(delivery, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateDriverLocationCommandHandler(mockRepo.Object);
        var command = new UpdateDriverLocationCommand
        {
            DeliveryId = delivery.Id,
            DriverId = driverId,
            Latitude = 51.5030,
            Longitude = -0.1300
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryId.Should().Be(delivery.Id);
        result.EstimatedArrival.Should().NotBeNull();
        result.LastLocationUpdate.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        mockRepo.Verify(r => r.SaveAsync(delivery, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
