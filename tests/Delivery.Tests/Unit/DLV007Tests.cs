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
/// Unit tests for DLV-007: Get Estimated Arrival.
/// Tests the GetEstimatedArrivalQueryHandler.
/// </summary>
public class DLV007Tests
{
    #region Test Helpers

    private static DeliveryAggregate CreateAssignedDelivery()
    {
        var delivery = new DeliveryAggregate(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            route: new Route(
                new Location("10 Restaurant Street", "London", "EC1A 1BB", 51.5074, -0.1278),
                new Location("20 Customer Road", "London", "SW1A 2AA", 51.5014, -0.1419)),
            readyAt: DateTimeOffset.UtcNow);

        delivery.AssignDriver(Guid.NewGuid(), 51.5080, -0.1280);
        delivery.ClearDomainEvents();

        return delivery;
    }

    #endregion

    /// <summary>
    /// AC: DLV-007-AC-01
    /// When a driver is assigned, the estimated arrival time is returned.
    /// </summary>
    [Fact]
    public async Task GetEstimatedArrival_WhenDriverAssigned_ReturnsEstimatedArrival()
    {
        // Arrange
        var delivery = CreateAssignedDelivery();

        var mockRepo = new Mock<IDeliveryRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);

        var handler = new GetEstimatedArrivalQueryHandler(mockRepo.Object);
        var query = new GetEstimatedArrivalQuery { DeliveryId = delivery.Id };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DeliveryId.Should().Be(delivery.Id);
        result.EstimatedArrivalTime.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(-1));
        result.EstimatedMinutes.Should().BeGreaterThanOrEqualTo(0);
        result.LastLocationUpdate.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-007-ERR-01
    /// When the delivery does not exist, the handler throws DeliveryNotFoundException.
    /// </summary>
    [Fact]
    public async Task GetEstimatedArrival_WhenDeliveryNotFound_ThrowsDeliveryNotFoundException()
    {
        // Arrange
        var mockRepo = new Mock<IDeliveryRepository>();
        var deliveryId = Guid.NewGuid();

        mockRepo
            .Setup(r => r.GetByIdAsync(deliveryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryAggregate?)null);

        var handler = new GetEstimatedArrivalQueryHandler(mockRepo.Object);
        var query = new GetEstimatedArrivalQuery { DeliveryId = deliveryId };

        // Act
        var act = async () => await handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DeliveryNotFoundException>()
            .Where(ex => ex.DeliveryId == deliveryId)
            .Where(ex => ex.ErrorCode == "DELIVERY_NOT_FOUND");
    }

    /// <summary>
    /// AC: DLV-007-ERR-02
    /// When no driver is assigned, the handler throws InvalidDeliveryStateException with DELIVERY_NO_DRIVER.
    /// </summary>
    [Fact]
    public async Task GetEstimatedArrival_WhenNoDriverAssigned_ThrowsInvalidDeliveryStateException()
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

        var mockRepo = new Mock<IDeliveryRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);

        var handler = new GetEstimatedArrivalQueryHandler(mockRepo.Object);
        var query = new GetEstimatedArrivalQuery { DeliveryId = delivery.Id };

        // Act
        var act = async () => await handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDeliveryStateException>()
            .Where(ex => ex.ErrorCode == "DELIVERY_NO_DRIVER");
    }
}
