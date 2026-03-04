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
/// Unit tests for RST-004: Accept Order.
/// Tests the RestaurantOrder aggregate Accept() method and AcceptOrderCommandHandler.
/// </summary>
public class RST004Tests
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

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: RST-004-AC-01
    /// When a pending order is accepted with valid prep time, it transitions to Accepted.
    /// </summary>
    [Fact]
    public void AcceptOrder_WhenPending_TransitionsToAccepted()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Accept(30);

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Accepted);
        order.EstimatedPrepTime.Should().Be(30);
        order.AcceptedAt.Should().NotBeNull();
        order.AcceptedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: RST-004-AC-02
    /// When a pending order is accepted, an OrderAccepted event is raised.
    /// </summary>
    [Fact]
    public void AcceptOrder_WhenPending_RaisesOrderAcceptedEvent()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Accept(25);

        // Assert
        order.DomainEvents.Should().HaveCount(1);
        var domainEvent = order.DomainEvents[0];
        domainEvent.Should().BeOfType<OrderAccepted>();

        var acceptedEvent = (OrderAccepted)domainEvent;
        acceptedEvent.OrderId.Should().Be(order.Id);
        acceptedEvent.RestaurantId.Should().Be(order.RestaurantId);
        acceptedEvent.EstimatedPrepMinutes.Should().Be(25);
        acceptedEvent.AcceptedAt.Should().Be(order.AcceptedAt!.Value);
    }

    /// <summary>
    /// AC: RST-004-ERR-03 (RST-R03)
    /// When prep time is below 5 minutes, ArgumentOutOfRangeException is thrown.
    /// </summary>
    [Fact]
    public void AcceptOrder_WhenPrepTimeTooLow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        var act = () => order.Accept(4);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        order.Status.Should().Be(RestaurantOrderStatus.Pending);
        order.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// AC: RST-004-ERR-03 (RST-R03)
    /// When prep time is above 90 minutes, ArgumentOutOfRangeException is thrown.
    /// </summary>
    [Fact]
    public void AcceptOrder_WhenPrepTimeTooHigh_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        var act = () => order.Accept(91);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        order.Status.Should().Be(RestaurantOrderStatus.Pending);
        order.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// AC: RST-004-ERR-02
    /// When the order is not in Pending status, Accept throws InvalidOrderStateException.
    /// </summary>
    [Fact]
    public void AcceptOrder_WhenNotPending_ThrowsInvalidOrderStateException()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Accepted);

        // Act
        var act = () => order.Accept(30);

        // Assert
        act.Should().Throw<InvalidOrderStateException>()
            .Where(ex => ex.ErrorCode == "RESTAURANT_ORDER_NOT_PENDING")
            .Where(ex => ex.CurrentStatus == RestaurantOrderStatus.Accepted);
    }

    /// <summary>
    /// AC: RST-004-AC-03 (boundary)
    /// Prep time of exactly 5 minutes is valid.
    /// </summary>
    [Fact]
    public void AcceptOrder_WithPrepTime5_IsValid()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Accept(5);

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Accepted);
        order.EstimatedPrepTime.Should().Be(5);
    }

    /// <summary>
    /// AC: RST-004-AC-03 (boundary)
    /// Prep time of exactly 90 minutes is valid.
    /// </summary>
    [Fact]
    public void AcceptOrder_WithPrepTime90_IsValid()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Accept(90);

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Accepted);
        order.EstimatedPrepTime.Should().Be(90);
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: RST-004-ERR-01
    /// When the order does not exist, the handler throws RestaurantOrderNotFoundException.
    /// </summary>
    [Fact]
    public async Task AcceptOrder_WhenOrderNotFound_ThrowsRestaurantOrderNotFoundException()
    {
        // Arrange
        var mockRepo = new Mock<IRestaurantOrderRepository>();
        var orderId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetByIdAsync(orderId, restaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RestaurantOrder?)null);

        var handler = new AcceptOrderCommandHandler(mockRepo.Object);
        var command = new AcceptOrderCommand
        {
            OrderId = orderId,
            RestaurantId = restaurantId,
            EstimatedPrepMinutes = 30
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<RestaurantOrderNotFoundException>()
            .Where(ex => ex.OrderId == orderId)
            .Where(ex => ex.ErrorCode == "RESTAURANT_ORDER_NOT_FOUND");
    }

    /// <summary>
    /// AC: RST-004-AC-04
    /// When acceptance succeeds, the handler returns the correct result and saves.
    /// </summary>
    [Fact]
    public async Task AcceptOrder_WhenSuccessful_ReturnResultAndSaves()
    {
        // Arrange
        var order = CreateTestOrder();
        var mockRepo = new Mock<IRestaurantOrderRepository>();

        mockRepo.Setup(r => r.GetByIdAsync(order.Id, order.RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        mockRepo.Setup(r => r.SaveAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AcceptOrderCommandHandler(mockRepo.Object);
        var command = new AcceptOrderCommand
        {
            OrderId = order.Id,
            RestaurantId = order.RestaurantId,
            EstimatedPrepMinutes = 20
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.Status.Should().Be("Accepted");
        result.EstimatedPrepMinutes.Should().Be(20);
        result.AcceptedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        mockRepo.Verify(r => r.SaveAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
