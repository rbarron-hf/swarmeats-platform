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
/// Unit tests for RST-005: Reject Order.
/// Tests the RestaurantOrder aggregate Reject() method and RejectOrderCommandHandler.
/// </summary>
public class RST005Tests
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
    /// AC: RST-005-AC-01
    /// When a pending order is rejected with a valid reason, it transitions to Rejected.
    /// </summary>
    [Fact]
    public void RejectOrder_WhenPending_TransitionsToRejected()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Reject("TOO_BUSY", "Kitchen is at capacity");

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Rejected);
        order.RejectionReason.Should().Be("TOO_BUSY");
        order.RejectionNotes.Should().Be("Kitchen is at capacity");
        order.RejectedAt.Should().NotBeNull();
        order.RejectedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: RST-005-AC-02
    /// When a pending order is rejected, an OrderRejected event is raised.
    /// </summary>
    [Fact]
    public void RejectOrder_WhenPending_RaisesOrderRejectedEvent()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Reject("OTHER", "Test notes");

        // Assert
        order.DomainEvents.Should().HaveCount(1);
        var domainEvent = order.DomainEvents[0];
        domainEvent.Should().BeOfType<OrderRejected>();

        var rejectedEvent = (OrderRejected)domainEvent;
        rejectedEvent.OrderId.Should().Be(order.Id);
        rejectedEvent.RestaurantId.Should().Be(order.RestaurantId);
        rejectedEvent.ReasonCode.Should().Be("OTHER");
        rejectedEvent.RejectedAt.Should().Be(order.RejectedAt!.Value);
    }

    /// <summary>
    /// AC: RST-005-ERR-03 (RST-R04)
    /// When an invalid reason code is provided, ArgumentException is thrown.
    /// </summary>
    [Fact]
    public void RejectOrder_WhenInvalidReasonCode_ThrowsArgumentException()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        var act = () => order.Reject("INVALID_REASON");

        // Assert
        act.Should().Throw<ArgumentException>();
        order.Status.Should().Be(RestaurantOrderStatus.Pending);
        order.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// AC: RST-005-ERR-02 (RST-R05)
    /// When the order is not in Pending status, Reject throws InvalidOrderStateException.
    /// </summary>
    [Fact]
    public void RejectOrder_WhenNotPending_ThrowsInvalidOrderStateException()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Accepted);

        // Act
        var act = () => order.Reject("TOO_BUSY");

        // Assert
        act.Should().Throw<InvalidOrderStateException>()
            .Where(ex => ex.ErrorCode == "RESTAURANT_ORDER_NOT_PENDING")
            .Where(ex => ex.CurrentStatus == RestaurantOrderStatus.Accepted);
    }

    /// <summary>
    /// AC: RST-005-AC-03
    /// All four valid reason codes are accepted.
    /// </summary>
    [Theory]
    [InlineData("RESTAURANT_CLOSED")]
    [InlineData("ITEM_UNAVAILABLE")]
    [InlineData("TOO_BUSY")]
    [InlineData("OTHER")]
    public void RejectOrder_WithValidReasonCodes_Succeeds(string reasonCode)
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Reject(reasonCode);

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Rejected);
        order.RejectionReason.Should().Be(reasonCode);
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: RST-005-ERR-01
    /// When the order does not exist, the handler throws RestaurantOrderNotFoundException.
    /// </summary>
    [Fact]
    public async Task RejectOrder_WhenOrderNotFound_ThrowsRestaurantOrderNotFoundException()
    {
        // Arrange
        var mockRepo = new Mock<IRestaurantOrderRepository>();
        var orderId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetByIdAsync(orderId, restaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RestaurantOrder?)null);

        var handler = new RejectOrderCommandHandler(mockRepo.Object);
        var command = new RejectOrderCommand
        {
            OrderId = orderId,
            RestaurantId = restaurantId,
            ReasonCode = "TOO_BUSY"
        };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<RestaurantOrderNotFoundException>()
            .Where(ex => ex.OrderId == orderId)
            .Where(ex => ex.ErrorCode == "RESTAURANT_ORDER_NOT_FOUND");
    }

    /// <summary>
    /// AC: RST-005-AC-04
    /// When rejection succeeds, the handler returns the correct result and saves.
    /// </summary>
    [Fact]
    public async Task RejectOrder_WhenSuccessful_ReturnsResultAndSaves()
    {
        // Arrange
        var order = CreateTestOrder();
        var mockRepo = new Mock<IRestaurantOrderRepository>();

        mockRepo.Setup(r => r.GetByIdAsync(order.Id, order.RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        mockRepo.Setup(r => r.SaveAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RejectOrderCommandHandler(mockRepo.Object);
        var command = new RejectOrderCommand
        {
            OrderId = order.Id,
            RestaurantId = order.RestaurantId,
            ReasonCode = "TOO_BUSY",
            Notes = "Very busy tonight"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.Status.Should().Be("Rejected");
        result.ReasonCode.Should().Be("TOO_BUSY");
        result.Notes.Should().Be("Very busy tonight");
        result.RejectedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        mockRepo.Verify(r => r.SaveAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
