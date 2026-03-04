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
/// Unit tests for DLV-005: Complete Delivery.
/// Tests the Delivery aggregate Complete() method, SLA calculation, and the handler.
/// </summary>
public class DLV005Tests
{
    #region Test Helpers

    private static DeliveryAggregate CreatePickedUpDelivery(
        Guid? driverId = null,
        DateTimeOffset? readyAt = null)
    {
        var delivery = new DeliveryAggregate(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            route: new Route(
                new Location("10 Restaurant Street", "London", "EC1A 1BB", 51.5074, -0.1278),
                new Location("20 Customer Road", "London", "SW1A 2AA", 51.5014, -0.1419)),
            readyAt: readyAt ?? DateTimeOffset.UtcNow);

        var assignedDriverId = driverId ?? Guid.NewGuid();
        delivery.AssignDriver(assignedDriverId, 51.5080, -0.1280);
        delivery.ClearDomainEvents();
        delivery.ConfirmPickup(assignedDriverId);

        return delivery;
    }

    #endregion

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: DLV-005-AC-01
    /// When completed by the assigned driver, status transitions from PickedUp to Delivered.
    /// </summary>
    [Fact]
    public void CompleteDelivery_WhenPickedUp_TransitionsToDelivered()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreatePickedUpDelivery(driverId);

        // Act
        delivery.Complete(driverId);

        // Assert
        delivery.Status.Should().Be(DeliveryStatus.Delivered);
        delivery.DeliveredAt.Should().NotBeNull();
        delivery.DeliveredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: DLV-005-AC-02
    /// totalDeliveryMinutes is calculated as elapsed time from readyAt to deliveredAt.
    /// </summary>
    [Fact]
    public void CompleteDelivery_CalculatesTotalDeliveryMinutes()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var readyAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var delivery = CreatePickedUpDelivery(driverId, readyAt);

        // Act
        delivery.Complete(driverId);

        // Assert
        delivery.TotalDeliveryMinutes.Should().NotBeNull();
        delivery.TotalDeliveryMinutes!.Value.Should().BeGreaterThanOrEqualTo(19); // Allow minor timing variance
        delivery.TotalDeliveryMinutes!.Value.Should().BeLessThanOrEqualTo(21);
    }

    /// <summary>
    /// AC: DLV-005-AC-03
    /// slaBreached is false when total delivery time is within 45 minutes.
    /// </summary>
    [Fact]
    public void CompleteDelivery_WithinSla_SlaBreachedIsFalse()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var readyAt = DateTimeOffset.UtcNow.AddMinutes(-10); // Well within SLA
        var delivery = CreatePickedUpDelivery(driverId, readyAt);

        // Act
        delivery.Complete(driverId);

        // Assert
        delivery.SlaBreached.Should().NotBeNull();
        delivery.SlaBreached!.Value.Should().BeFalse();
    }

    /// <summary>
    /// AC: DLV-005-AC-03
    /// slaBreached is true when total delivery time exceeds 45 minutes.
    /// </summary>
    [Fact]
    public void CompleteDelivery_ExceedingSla_SlaBreachedIsTrue()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var readyAt = DateTimeOffset.UtcNow.AddMinutes(-50); // Exceeds 45-minute SLA
        var delivery = CreatePickedUpDelivery(driverId, readyAt);

        // Act
        delivery.Complete(driverId);

        // Assert
        delivery.SlaBreached.Should().NotBeNull();
        delivery.SlaBreached!.Value.Should().BeTrue();
        delivery.TotalDeliveryMinutes.Should().BeGreaterThan(45);
    }

    /// <summary>
    /// AC: DLV-005-AC-04
    /// A DeliveryCompleted domain event is raised with the correct payload.
    /// </summary>
    [Fact]
    public void CompleteDelivery_RaisesDeliveryCompletedEvent()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreatePickedUpDelivery(driverId);

        // Act
        delivery.Complete(driverId);

        // Assert
        delivery.DomainEvents.Should().HaveCount(1);
        var domainEvent = delivery.DomainEvents[0];
        domainEvent.Should().BeOfType<DeliveryCompleted>();

        var completedEvent = (DeliveryCompleted)domainEvent;
        completedEvent.DeliveryId.Should().Be(delivery.Id);
        completedEvent.OrderId.Should().Be(delivery.OrderId);
        completedEvent.DriverId.Should().Be(driverId);
        completedEvent.DeliveredAt.Should().Be(delivery.DeliveredAt!.Value);
        completedEvent.TotalDeliveryMinutes.Should().Be(delivery.TotalDeliveryMinutes!.Value);
        completedEvent.SlaBreached.Should().Be(delivery.SlaBreached!.Value);
        completedEvent.EventId.Should().NotBeEmpty();
    }

    /// <summary>
    /// AC: DLV-005-ERR-02
    /// When the delivery is not in PickedUp status, Complete throws.
    /// </summary>
    [Fact]
    public void CompleteDelivery_WhenDriverAssigned_ThrowsInvalidDeliveryStateException()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = new DeliveryAggregate(
            id: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            route: new Route(
                new Location("10 Restaurant Street", "London", "EC1A 1BB", 51.5074, -0.1278),
                new Location("20 Customer Road", "London", "SW1A 2AA", 51.5014, -0.1419)),
            readyAt: DateTimeOffset.UtcNow);
        delivery.AssignDriver(driverId, 51.5080, -0.1280);
        delivery.ClearDomainEvents();

        // Act - trying to complete without confirming pickup
        var act = () => delivery.Complete(driverId);

        // Assert
        act.Should().Throw<InvalidDeliveryStateException>()
            .Where(ex => ex.ErrorCode == "DELIVERY_NOT_PICKED_UP");
    }

    /// <summary>
    /// AC: DLV-005-ERR-03
    /// When the driver ID does not match, Complete throws WrongDriverException.
    /// </summary>
    [Fact]
    public void CompleteDelivery_WrongDriver_ThrowsWrongDriverException()
    {
        // Arrange
        var assignedDriverId = Guid.NewGuid();
        var wrongDriverId = Guid.NewGuid();
        var delivery = CreatePickedUpDelivery(assignedDriverId);

        // Act
        var act = () => delivery.Complete(wrongDriverId);

        // Assert
        act.Should().Throw<WrongDriverException>()
            .Where(ex => ex.ErrorCode == "DELIVERY_WRONG_DRIVER");

        delivery.Status.Should().Be(DeliveryStatus.PickedUp);
        delivery.DeliveredAt.Should().BeNull();
        delivery.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: DLV-005-ERR-01
    /// When the delivery does not exist, the handler throws DeliveryNotFoundException.
    /// </summary>
    [Fact]
    public async Task CompleteDelivery_WhenDeliveryNotFound_ThrowsDeliveryNotFoundException()
    {
        // Arrange
        var mockRepo = new Mock<IDeliveryRepository>();
        var deliveryId = Guid.NewGuid();

        mockRepo
            .Setup(r => r.GetByIdAsync(deliveryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryAggregate?)null);

        var handler = new CompleteDeliveryCommandHandler(mockRepo.Object);
        var command = new CompleteDeliveryCommand
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
    /// On successful completion, the handler returns the result with SLA details.
    /// </summary>
    [Fact]
    public async Task CompleteDelivery_ReturnsResultWithSlaDetails()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var delivery = CreatePickedUpDelivery(driverId);

        var mockRepo = new Mock<IDeliveryRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(delivery.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(delivery);
        mockRepo.Setup(r => r.SaveAsync(delivery, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CompleteDeliveryCommandHandler(mockRepo.Object);
        var command = new CompleteDeliveryCommand
        {
            DeliveryId = delivery.Id,
            DriverId = driverId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Delivered");
        result.DeliveredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.TotalDeliveryMinutes.Should().BeGreaterThanOrEqualTo(0);

        mockRepo.Verify(r => r.SaveAsync(delivery, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
