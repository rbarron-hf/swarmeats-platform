using FluentAssertions;
using Moq;
using Delivery.Domain.Aggregates;
using Delivery.Domain.Exceptions;
using Delivery.Domain.Queries;
using Delivery.Domain.ValueObjects;
using Delivery.Infrastructure.Repositories;
using Xunit;

namespace Delivery.Tests.Unit;

/// <summary>
/// Unit tests for DLV-006: Get Delivery Status.
/// Tests the GetDeliveryStatusQueryHandler.
/// </summary>
public class DLV006Tests
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

        return delivery;
    }

    #endregion

    /// <summary>
    /// AC: DLV-006-AC-01
    /// When a delivery exists, the full delivery status is returned.
    /// </summary>
    [Fact]
    public async Task GetDeliveryStatus_WhenDeliveryExists_ReturnsFullDeliveryStatus()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.AwaitingDriver);

        var mockRepo = new Mock<IDeliveryRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);

        var handler = new GetDeliveryStatusQueryHandler(mockRepo.Object);
        var query = new GetDeliveryStatusQuery { DeliveryId = delivery.Id };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryId.Should().Be(delivery.Id);
        result.OrderId.Should().Be(delivery.OrderId);
        result.Status.Should().Be("AwaitingDriver");
        result.DriverId.Should().BeNull();
        result.DriverLocation.Should().BeNull();
        result.EstimatedArrival.Should().BeNull();
        result.Route.Should().NotBeNull();
        result.Route.Pickup.Street.Should().Be("10 Restaurant Street");
        result.Route.Dropoff.Street.Should().Be("20 Customer Road");
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-006-AC-02
    /// When a delivery has a driver assigned, driver location and ETA are included.
    /// </summary>
    [Fact]
    public async Task GetDeliveryStatus_WhenDriverAssigned_IncludesDriverLocationAndEta()
    {
        // Arrange
        var delivery = CreateTestDelivery(DeliveryStatus.DriverAssigned);

        var mockRepo = new Mock<IDeliveryRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);

        var handler = new GetDeliveryStatusQueryHandler(mockRepo.Object);
        var query = new GetDeliveryStatusQuery { DeliveryId = delivery.Id };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Status.Should().Be("DriverAssigned");
        result.DriverId.Should().NotBeNull();
        result.DriverLocation.Should().NotBeNull();
        result.EstimatedArrival.Should().NotBeNull();
    }

    /// <summary>
    /// AC: DLV-006-ERR-01
    /// When the delivery does not exist, the handler throws DeliveryNotFoundException.
    /// </summary>
    [Fact]
    public async Task GetDeliveryStatus_WhenDeliveryNotFound_ThrowsDeliveryNotFoundException()
    {
        // Arrange
        var mockRepo = new Mock<IDeliveryRepository>();
        var deliveryId = Guid.NewGuid();

        mockRepo
            .Setup(r => r.GetByIdAsync(deliveryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryAggregate?)null);

        var handler = new GetDeliveryStatusQueryHandler(mockRepo.Object);
        var query = new GetDeliveryStatusQuery { DeliveryId = deliveryId };

        // Act
        var act = async () => await handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DeliveryNotFoundException>()
            .Where(ex => ex.DeliveryId == deliveryId)
            .Where(ex => ex.ErrorCode == "DELIVERY_NOT_FOUND");
    }
}
