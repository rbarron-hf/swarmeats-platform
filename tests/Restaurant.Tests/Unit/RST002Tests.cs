using FluentAssertions;
using Moq;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.Queries;
using Restaurant.Domain.ValueObjects;
using Restaurant.Infrastructure.Repositories;
using Xunit;

namespace Restaurant.Tests.Unit;

/// <summary>
/// Unit tests for RST-002: Get Active Orders.
/// Tests the GetActiveOrdersQueryHandler and status filtering logic.
/// </summary>
public class RST002Tests
{
    #region Test Helpers

    private static RestaurantOrder CreateTestOrder(
        Guid? restaurantId = null,
        RestaurantOrderStatus status = RestaurantOrderStatus.Pending)
    {
        var restId = restaurantId ?? Guid.NewGuid();
        var order = new RestaurantOrder(
            orderId: Guid.NewGuid(),
            restaurantId: restId,
            orderNumber: "ORD-20260303-001",
            lineItems: new List<RestaurantOrderLineItem>
            {
                new(Guid.NewGuid(), "Margherita Pizza", 2, 8.99m)
            },
            sourceEventId: Guid.NewGuid());

        // Use reflection to set status for testing non-Pending states
        if (status != RestaurantOrderStatus.Pending)
        {
            var statusProperty = typeof(RestaurantOrder).GetProperty(nameof(RestaurantOrder.Status));
            statusProperty!.SetValue(order, status);
        }

        return order;
    }

    #endregion

    /// <summary>
    /// AC: RST-002-AC-01
    /// When no status filter is provided, all orders for the restaurant are returned.
    /// </summary>
    [Fact]
    public async Task GetActiveOrders_WithoutStatusFilter_ReturnsAllOrders()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var orders = new List<RestaurantOrder>
        {
            CreateTestOrder(restaurantId, RestaurantOrderStatus.Pending),
            CreateTestOrder(restaurantId, RestaurantOrderStatus.Accepted)
        };

        var mockRepo = new Mock<IRestaurantOrderRepository>();
        mockRepo.Setup(r => r.GetByRestaurantIdAsync(restaurantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        var handler = new GetActiveOrdersQueryHandler(mockRepo.Object);
        var query = new GetActiveOrdersQuery { RestaurantId = restaurantId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Orders.Should().HaveCount(2);
    }

    /// <summary>
    /// AC: RST-002-AC-02
    /// When a status filter is provided, only orders matching that status are returned.
    /// </summary>
    [Fact]
    public async Task GetActiveOrders_WithStatusFilter_ReturnsFilteredOrders()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var pendingOrders = new List<RestaurantOrder>
        {
            CreateTestOrder(restaurantId, RestaurantOrderStatus.Pending)
        };

        var mockRepo = new Mock<IRestaurantOrderRepository>();
        mockRepo.Setup(r => r.GetByRestaurantIdAsync(restaurantId, RestaurantOrderStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingOrders);

        var handler = new GetActiveOrdersQueryHandler(mockRepo.Object);
        var query = new GetActiveOrdersQuery
        {
            RestaurantId = restaurantId,
            Status = RestaurantOrderStatus.Pending
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Orders.Should().HaveCount(1);
        result.Orders[0].Status.Should().Be("Pending");
    }

    /// <summary>
    /// AC: RST-002-AC-03
    /// Each order summary includes the expected fields.
    /// </summary>
    [Fact]
    public async Task GetActiveOrders_OrderSummaryContainsExpectedFields()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var order = CreateTestOrder(restaurantId);
        var mockRepo = new Mock<IRestaurantOrderRepository>();
        mockRepo.Setup(r => r.GetByRestaurantIdAsync(restaurantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RestaurantOrder> { order });

        var handler = new GetActiveOrdersQueryHandler(mockRepo.Object);
        var query = new GetActiveOrdersQuery { RestaurantId = restaurantId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var summary = result.Orders[0];
        summary.OrderId.Should().Be(order.Id);
        summary.OrderNumber.Should().Be("ORD-20260303-001");
        summary.Status.Should().Be("Pending");
        summary.LineItems.Should().HaveCount(1);
        summary.LineItems[0].MenuItemName.Should().Be("Margherita Pizza");
        summary.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: RST-002-AC-04
    /// When no orders exist for the restaurant, an empty list is returned.
    /// </summary>
    [Fact]
    public async Task GetActiveOrders_WhenNoOrders_ReturnsEmptyList()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var mockRepo = new Mock<IRestaurantOrderRepository>();
        mockRepo.Setup(r => r.GetByRestaurantIdAsync(restaurantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RestaurantOrder>());

        var handler = new GetActiveOrdersQueryHandler(mockRepo.Object);
        var query = new GetActiveOrdersQuery { RestaurantId = restaurantId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Orders.Should().BeEmpty();
    }
}
