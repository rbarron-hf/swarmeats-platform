using FluentAssertions;
using Moq;
using Delivery.Domain.Aggregates;
using Delivery.Domain.Commands;
using Delivery.Domain.Events;
using Delivery.Domain.Exceptions;
using Delivery.Domain.ValueObjects;
using Delivery.Infrastructure.Repositories;
using Xunit;

namespace Delivery.Tests.Unit;

/// <summary>
/// Unit tests for DLV-002: Assign Driver.
/// Tests the Delivery aggregate AssignDriver() method, the AssignDriverCommandHandler,
/// and error conditions including DLV-R01 (driver availability) and DLV-R02 (distance check).
/// </summary>
public class DLV002Tests
{
    #region Test Helpers

    /// <summary>
    /// Restaurant location: Central London (51.5074, -0.1278).
    /// </summary>
    private static readonly Location RestaurantLocation = new("10 Restaurant Street", "London", "EC1A 1BB", 51.5074, -0.1278);

    /// <summary>
    /// Customer location: Near Waterloo (51.5014, -0.1140).
    /// </summary>
    private static readonly Location CustomerLocation = new("20 Customer Road", "London", "SE1 8XX", 51.5014, -0.1140);

    /// <summary>
    /// Creates a test Delivery aggregate in the specified status.
    /// Uses reflection to set the Status property for testing non-AwaitingDriver states.
    /// </summary>
    private static DeliveryAggregate CreateTestDelivery(DeliveryStatus status = DeliveryStatus.AwaitingDriver)
    {
        var delivery = new DeliveryAggregate(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            route: new Route(RestaurantLocation, CustomerLocation),
            readyAt: DateTimeOffset.UtcNow);

        if (status != DeliveryStatus.AwaitingDriver)
        {
            var statusProperty = typeof(DeliveryAggregate).GetProperty(nameof(DeliveryAggregate.Status));
            statusProperty!.SetValue(delivery, status);
        }

        return delivery;
    }

    #endregion

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: DLV-002-AC-01
    /// When a delivery is in AwaitingDriver status and a valid driver is assigned,
    /// the status transitions to DriverAssigned.
    /// </summary>
    [Fact]
    public void AssignDriver_WhenAwaitingDriver_TransitionsToDriverAssigned()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);
        var driverId = Guid.NewGuid();
        // Driver near restaurant (within 5km)
        var driverLat = 51.5080;
        var driverLon = -0.1280;

        // Act
        delivery.AssignDriver(driverId, driverLat, driverLon);

        // Assert
        delivery.Status.Should().Be(DeliveryStatus.DriverAssigned);
        delivery.DriverId.Should().Be(driverId);
        delivery.DriverAssignedAt.Should().NotBeNull();
        delivery.DriverAssignedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-002-AC-02
    /// The driver's initial location is stored as the current DriverLocation.
    /// </summary>
    [Fact]
    public void AssignDriver_StoresDriverLocation()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);
        var driverId = Guid.NewGuid();
        var driverLat = 51.5080;
        var driverLon = -0.1280;

        // Act
        delivery.AssignDriver(driverId, driverLat, driverLon);

        // Assert
        delivery.DriverLocation.Should().NotBeNull();
        delivery.DriverLocation!.Latitude.Should().Be(driverLat);
        delivery.DriverLocation!.Longitude.Should().Be(driverLon);
        delivery.DriverLocation!.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-002-AC-03
    /// The EstimatedArrival is calculated based on driver's location and delivery route.
    /// </summary>
    [Fact]
    public void AssignDriver_CalculatesEstimatedArrival()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);
        var driverId = Guid.NewGuid();
        var driverLat = 51.5080;
        var driverLon = -0.1280;

        // Act
        delivery.AssignDriver(driverId, driverLat, driverLon);

        // Assert
        delivery.EstimatedArrival.Should().NotBeNull();
        delivery.EstimatedArrival!.EstimatedMinutes.Should().BeGreaterThanOrEqualTo(0);
        delivery.EstimatedArrival!.EstimatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(-1));
    }

    /// <summary>
    /// AC: DLV-002-AC-04
    /// A DriverAssigned domain event is raised with the correct payload.
    /// </summary>
    [Fact]
    public void AssignDriver_RaisesDriverAssignedEvent()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);
        var driverId = Guid.NewGuid();
        var driverLat = 51.5080;
        var driverLon = -0.1280;

        // Act
        delivery.AssignDriver(driverId, driverLat, driverLon);

        // Assert
        delivery.DomainEvents.Should().HaveCount(1);
        var domainEvent = delivery.DomainEvents[0];
        domainEvent.Should().BeOfType<DriverAssigned>();

        var assignedEvent = (DriverAssigned)domainEvent;
        assignedEvent.DeliveryId.Should().Be(delivery.Id);
        assignedEvent.OrderId.Should().Be(delivery.OrderId);
        assignedEvent.DriverId.Should().Be(driverId);
        assignedEvent.EstimatedArrivalMinutes.Should().BeGreaterThanOrEqualTo(0);
        assignedEvent.AssignedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        assignedEvent.EventId.Should().NotBeEmpty();
        assignedEvent.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-002-ERR-02
    /// When the delivery is already assigned, AssignDriver throws InvalidDeliveryStateException.
    /// </summary>
    [Fact]
    public void AssignDriver_WhenAlreadyAssigned_ThrowsInvalidDeliveryStateException()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.DriverAssigned);
        var driverId = Guid.NewGuid();

        // Act
        var act = () => delivery.AssignDriver(driverId, 51.5080, -0.1280);

        // Assert
        act.Should().Throw<InvalidDeliveryStateException>()
            .Where(ex => ex.DeliveryId == delivery.Id)
            .Where(ex => ex.CurrentStatus == DeliveryStatus.DriverAssigned)
            .Where(ex => ex.ErrorCode == "DELIVERY_ALREADY_ASSIGNED");

        // Verify aggregate state is unmodified
        delivery.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// AC: DLV-002-ERR-04 (DLV-R02)
    /// When the driver is more than 5km from the restaurant, AssignDriver throws DriverTooFarException.
    /// </summary>
    [Fact]
    public void AssignDriver_WhenDriverTooFar_ThrowsDriverTooFarException()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);
        var driverId = Guid.NewGuid();
        // Driver in Paris (well over 5km from London restaurant)
        var driverLat = 48.8566;
        var driverLon = 2.3522;

        // Act
        var act = () => delivery.AssignDriver(driverId, driverLat, driverLon);

        // Assert
        act.Should().Throw<DriverTooFarException>()
            .Where(ex => ex.DriverId == driverId)
            .Where(ex => ex.DistanceKm > 5.0)
            .Where(ex => ex.ErrorCode == "DRIVER_TOO_FAR");

        // Verify aggregate state is unmodified
        delivery.Status.Should().Be(DeliveryStatus.AwaitingDriver);
        delivery.DriverId.Should().BeNull();
        delivery.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: DLV-002-ERR-01
    /// When the delivery does not exist, the handler throws DeliveryNotFoundException.
    /// </summary>
    [Fact]
    public async Task AssignDriver_WhenDeliveryNotFound_ThrowsDeliveryNotFoundException()
    {
        // Arrange
        var mockDeliveryRepo = new Mock<IDeliveryRepository>();
        var mockDriverRepo = new Mock<IDriverRepository>();
        var deliveryId = Guid.NewGuid();

        mockDeliveryRepo
            .Setup(r => r.GetByIdAsync(deliveryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryAggregate?)null);

        var handler = new AssignDriverCommandHandler(mockDeliveryRepo.Object, mockDriverRepo.Object);
        var command = new AssignDriverCommand
        {
            DeliveryId = deliveryId,
            DriverId = Guid.NewGuid(),
            DriverLatitude = 51.5080,
            DriverLongitude = -0.1280
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DeliveryNotFoundException>()
            .Where(ex => ex.DeliveryId == deliveryId)
            .Where(ex => ex.ErrorCode == "DELIVERY_NOT_FOUND");
    }

    /// <summary>
    /// AC: DLV-002-ERR-03 (DLV-R01)
    /// When the driver already has an active delivery, the handler throws DriverNotAvailableException.
    /// </summary>
    [Fact]
    public async Task AssignDriver_WhenDriverHasActiveDelivery_ThrowsDriverNotAvailableException()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);
        var driverId = Guid.NewGuid();

        var mockDeliveryRepo = new Mock<IDeliveryRepository>();
        var mockDriverRepo = new Mock<IDriverRepository>();

        mockDeliveryRepo
            .Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);

        mockDriverRepo
            .Setup(r => r.HasActiveDeliveryAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AssignDriverCommandHandler(mockDeliveryRepo.Object, mockDriverRepo.Object);
        var command = new AssignDriverCommand
        {
            DeliveryId = delivery.Id,
            DriverId = driverId,
            DriverLatitude = 51.5080,
            DriverLongitude = -0.1280
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DriverNotAvailableException>()
            .Where(ex => ex.DriverId == driverId)
            .Where(ex => ex.ErrorCode == "DRIVER_NOT_AVAILABLE");
    }

    /// <summary>
    /// AC: DLV-002-AC-05
    /// On successful assignment, the handler returns the updated delivery with DriverAssigned status.
    /// </summary>
    [Fact]
    public async Task AssignDriver_ReturnsUpdatedDeliveryWithDriverAssignedStatus()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);
        var driverId = Guid.NewGuid();

        var mockDeliveryRepo = new Mock<IDeliveryRepository>();
        var mockDriverRepo = new Mock<IDriverRepository>();

        mockDeliveryRepo
            .Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);

        mockDriverRepo
            .Setup(r => r.HasActiveDeliveryAsync(driverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mockDeliveryRepo
            .Setup(r => r.MigratePartitionKeyAsync(delivery, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AssignDriverCommandHandler(mockDeliveryRepo.Object, mockDriverRepo.Object);
        var command = new AssignDriverCommand
        {
            DeliveryId = delivery.Id,
            DriverId = driverId,
            DriverLatitude = 51.5080,
            DriverLongitude = -0.1280
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryId.Should().Be(delivery.Id);
        result.OrderId.Should().Be(delivery.OrderId);
        result.Status.Should().Be("DriverAssigned");
        result.DriverId.Should().Be(driverId);
        result.DriverLocation.Should().NotBeNull();
        result.Route.Should().NotBeNull();
        result.EstimatedArrival.Should().NotBeNull();

        // Verify partition key migration was called
        mockDeliveryRepo.Verify(r => r.MigratePartitionKeyAsync(delivery, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
