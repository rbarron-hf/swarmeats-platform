using FluentAssertions;
using Moq;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.Commands;
using Restaurant.Domain.Exceptions;
using Restaurant.Domain.ValueObjects;
using Restaurant.Infrastructure.Repositories;
using Xunit;

namespace Restaurant.Tests.Unit;

/// <summary>
/// Unit tests for RST-006: Mark Preparing.
/// Tests the RestaurantOrder aggregate MarkPreparing() method and MarkPreparingCommandHandler.
/// </summary>
public class RST006Tests
{
    #region Test Helpers

    private static RestaurantOrder CreateTestOrder(RestaurantOrderStatus status = RestaurantOrderStatus.Accepted)
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

        // Set to Accepted for MarkPreparing tests
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
    /// AC: RST-006-AC-01
    /// When an accepted order is marked as preparing, it transitions to Preparing.
    /// </summary>
    [Fact]
    public void MarkPreparing_WhenAccepted_TransitionsToPreparing()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Accepted);

        // Act
        order.MarkPreparing();

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Preparing);
        order.PreparingAt.Should().NotBeNull();
        order.PreparingAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: RST-006-AC-02
    /// No domain event is raised for the Preparing transition.
    /// </summary>
    [Fact]
    public void MarkPreparing_DoesNotRaiseDomainEvent()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Accepted);

        // Act
        order.MarkPreparing();

        // Assert
        order.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// AC: RST-006-ERR-02
    /// When the order is not in Accepted status, MarkPreparing throws InvalidOrderStateException.
    /// </summary>
    [Fact]
    public void MarkPreparing_WhenNotAccepted_ThrowsInvalidOrderStateException()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Pending);

        // Act
        var act = () => order.MarkPreparing();

        // Assert
        act.Should().Throw<InvalidOrderStateException>()
            .Where(ex => ex.ErrorCode == "RESTAURANT_INVALID_TRANSITION")
            .Where(ex => ex.CurrentStatus == RestaurantOrderStatus.Pending);

        order.Status.Should().Be(RestaurantOrderStatus.Pending);
        order.PreparingAt.Should().BeNull();
    }

    /// <summary>
    /// AC: RST-006-ERR-02
    /// When the order is already Preparing, MarkPreparing throws InvalidOrderStateException.
    /// </summary>
    [Fact]
    public void MarkPreparing_WhenAlreadyPreparing_ThrowsInvalidOrderStateException()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Preparing);

        // Act
        var act = () => order.MarkPreparing();

        // Assert
        act.Should().Throw<InvalidOrderStateException>()
            .Where(ex => ex.CurrentStatus == RestaurantOrderStatus.Preparing);
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: RST-006-ERR-01
    /// When the order does not exist, the handler throws RestaurantOrderNotFoundException.
    /// </summary>
    [Fact]
    public async Task MarkPreparing_WhenOrderNotFound_ThrowsRestaurantOrderNotFoundException()
    {
        // Arrange
        var mockRepo = new Mock<IRestaurantOrderRepository>();
        var orderId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetByIdAsync(orderId, restaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RestaurantOrder?)null);

        var handler = new MarkPreparingCommandHandler(mockRepo.Object);
        var command = new MarkPreparingCommand
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
    /// AC: RST-006-AC-03
    /// When successful, the handler returns the correct result and saves.
    /// </summary>
    [Fact]
    public async Task MarkPreparing_WhenSuccessful_ReturnsResultAndSaves()
    {
        // Arrange
        var order = CreateTestOrder(RestaurantOrderStatus.Accepted);
        var mockRepo = new Mock<IRestaurantOrderRepository>();

        mockRepo.Setup(r => r.GetByIdAsync(order.Id, order.RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        mockRepo.Setup(r => r.SaveAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new MarkPreparingCommandHandler(mockRepo.Object);
        var command = new MarkPreparingCommand
        {
            OrderId = order.Id,
            RestaurantId = order.RestaurantId
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.Status.Should().Be("Preparing");
        result.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        mockRepo.Verify(r => r.SaveAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
