using FluentAssertions;
using Moq;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.Commands;
using Restaurant.Domain.Events;
using Restaurant.Domain.Exceptions;
using Restaurant.Domain.ValueObjects;
using Restaurant.Infrastructure.Repositories;
using Xunit;

namespace Restaurant.Tests.Unit;

/// <summary>
/// Unit tests for RST-007: Mark Ready for Pickup.
/// Tests the RestaurantOrder aggregate MarkReadyForPickup() method and MarkReadyForPickupCommandHandler.
/// </summary>
public class RST007Tests
{
    #region Test Helpers

    private static RestaurantOrder CreateTestOrder(RestaurantOrderStatus status = RestaurantOrderStatus.Preparing)
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

    private static RestaurantAddress CreateTestAddress()
    {
        return new RestaurantAddress("10 High Street", "London", "EC1A 1BB", 51.5155m, -0.0922m);
    }

    private static Menu CreateTestMenu(Guid restaurantId)
    {
        return new Menu(
            id: Guid.NewGuid(),
            restaurantId: restaurantId,
            restaurantName: "Test Restaurant",
            operatingHours: new OperatingHours("09:00", "22:00"),
            address: new RestaurantMenuAddress("10 High Street", "London", "EC1A 1BB", 51.5155m, -0.0922m));
    }

    #endregion

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: RST-007-AC-01
    /// When a preparing order is marked ready, it transitions to ReadyForPickup.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenPreparing_TransitionsToReadyForPickup()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Preparing);
        var address = CreateTestAddress();

        // Act
        order.MarkReadyForPickup(address);

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.ReadyForPickup);
        order.ReadyAt.Should().NotBeNull();
        order.ReadyAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: RST-007-AC-02
    /// When marked ready, an OrderReadyForPickup event is raised with restaurant address.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenPreparing_RaisesOrderReadyForPickupEvent()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Preparing);
        var address = CreateTestAddress();

        // Act
        order.MarkReadyForPickup(address);

        // Assert
        order.DomainEvents.Should().HaveCount(1);
        var domainEvent = order.DomainEvents[0];
        domainEvent.Should().BeOfType<OrderReadyForPickup>();

        var readyEvent = (OrderReadyForPickup)domainEvent;
        readyEvent.OrderId.Should().Be(order.Id);
        readyEvent.RestaurantId.Should().Be(order.RestaurantId);
        readyEvent.RestaurantAddress.Street.Should().Be("10 High Street");
        readyEvent.RestaurantAddress.City.Should().Be("London");
        readyEvent.RestaurantAddress.Postcode.Should().Be("EC1A 1BB");
        readyEvent.ReadyAt.Should().Be(order.ReadyAt!.Value);
    }

    /// <summary>
    /// AC: RST-007-ERR-02
    /// When the order is not in Preparing status, MarkReadyForPickup throws InvalidOrderStateException.
    /// </summary>
    [Fact]
    public void MarkReadyForPickup_WhenNotPreparing_ThrowsInvalidOrderStateException()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Accepted);
        var address = CreateTestAddress();

        // Act
        var act = () => order.MarkReadyForPickup(address);

        // Assert
        act.Should().Throw<InvalidOrderStateException>()
            .Where(ex => ex.ErrorCode == "RESTAURANT_INVALID_TRANSITION")
            .Where(ex => ex.CurrentStatus == RestaurantOrderStatus.Accepted);

        order.Status.Should().Be(RestaurantOrderStatus.Accepted);
        order.ReadyAt.Should().BeNull();
        order.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: RST-007-ERR-01
    /// When the order does not exist, the handler throws RestaurantOrderNotFoundException.
    /// </summary>
    [Fact]
    public async Task MarkReadyForPickup_WhenOrderNotFound_ThrowsRestaurantOrderNotFoundException()
    {
        // Arrange
        var mockOrderRepo = new Mock<IRestaurantOrderRepository>();
        var mockMenuRepo = new Mock<IMenuRepository>();
        var orderId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();

        mockOrderRepo.Setup(r => r.GetByIdAsync(orderId, restaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RestaurantOrder?)null);

        var handler = new MarkReadyForPickupCommandHandler(mockOrderRepo.Object, mockMenuRepo.Object);
        var command = new MarkReadyForPickupCommand
        {
            OrderId = orderId,
            RestaurantId = restaurantId
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<RestaurantOrderNotFoundException>()
            .Where(ex => ex.OrderId == orderId);
    }

    /// <summary>
    /// AC: RST-007-AC-03
    /// When successful, the handler loads the menu for the restaurant address,
    /// returns the correct result, and saves.
    /// </summary>
    [Fact]
    public async Task MarkReadyForPickup_WhenSuccessful_ReturnsResultAndSaves()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Preparing);
        var menu = CreateTestMenu(order.RestaurantId);

        var mockOrderRepo = new Mock<IRestaurantOrderRepository>();
        var mockMenuRepo = new Mock<IMenuRepository>();

        mockOrderRepo.Setup(r => r.GetByIdAsync(order.Id, order.RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        mockOrderRepo.Setup(r => r.SaveAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockMenuRepo.Setup(r => r.GetByRestaurantIdAsync(order.RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(menu);

        var handler = new MarkReadyForPickupCommandHandler(mockOrderRepo.Object, mockMenuRepo.Object);
        var command = new MarkReadyForPickupCommand
        {
            OrderId = order.Id,
            RestaurantId = order.RestaurantId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.Status.Should().Be("ReadyForPickup");
        result.ReadyAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        mockOrderRepo.Verify(r => r.SaveAsync(order, It.IsAny<CancellationToken>()), Times.Once);
        mockMenuRepo.Verify(r => r.GetByRestaurantIdAsync(order.RestaurantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
