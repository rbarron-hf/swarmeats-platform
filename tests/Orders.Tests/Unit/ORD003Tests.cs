using FluentAssertions;
using Moq;
using Orders.Domain.Aggregates;
using Orders.Domain.Exceptions;
using Orders.Domain.Queries;
using Orders.Domain.ValueObjects;
using Orders.Infrastructure.Repositories;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-003: Get Order by ID.
/// Tests the GetOrderQueryHandler mapping and error handling.
/// </summary>
public class ORD003Tests
{
    #region Test Helpers

    private static readonly Guid TestCustomerId = Guid.NewGuid();
    private static readonly Guid TestRestaurantId = Guid.NewGuid();
    private static readonly Guid TestMenuItemId = Guid.NewGuid();

    /// <summary>
    /// Creates a test Order aggregate in Placed status with known data for assertion.
    /// </summary>
    private static Order CreateTestOrder(Guid? orderId = null)
    {
        return new Order(
            id: orderId ?? Guid.NewGuid(),
            orderNumber: "ORD-20260303-042",
            customerId: TestCustomerId,
            restaurantId: TestRestaurantId,
            lineItems: new List<OrderLineItem>
            {
                new(
                    menuItemId: TestMenuItemId,
                    menuItemName: "Margherita Pizza",
                    quantity: 2,
                    unitPrice: 8.99m),
                new(
                    menuItemId: Guid.NewGuid(),
                    menuItemName: "Garlic Bread",
                    quantity: 1,
                    unitPrice: 3.50m)
            },
            deliveryAddress: new DeliveryAddress("123 High Street", "London", "SW1A 1AA", 51.5074, -0.1278),
            orderTotal: new OrderTotal(21.48m, 2.99m, 24.47m));
    }

    private static (GetOrderQueryHandler handler, Mock<IOrderRepository> repo) CreateHandler()
    {
        var mockRepo = new Mock<IOrderRepository>();
        var handler = new GetOrderQueryHandler(mockRepo.Object);
        return (handler, mockRepo);
    }

    #endregion

    #region Happy Path Tests

    /// <summary>
    /// AC: ORD-003-AC-01, ORD-003-AC-02
    /// When an order exists, the handler returns a GetOrderResponse with HTTP 200
    /// containing the full order details: id, number, customer, restaurant, status.
    /// </summary>
    [Fact]
    public async Task GetOrder_WhenOrderExists_ReturnsFullOrderDetails()
    {
        // Arrange
        var order = CreateTestOrder();
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderQuery { OrderId = order.Id };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.OrderNumber.Should().Be("ORD-20260303-042");
        result.CustomerId.Should().Be(TestCustomerId);
        result.RestaurantId.Should().Be(TestRestaurantId);
        result.Status.Should().Be("Placed");
    }

    /// <summary>
    /// AC: ORD-003-AC-01
    /// The response includes all line items with correct menuItemId, name, quantity, and unit price.
    /// </summary>
    [Fact]
    public async Task GetOrder_WhenOrderExists_ReturnsAllLineItems()
    {
        // Arrange
        var order = CreateTestOrder();
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderQuery { OrderId = order.Id };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.LineItems.Should().HaveCount(2);

        result.LineItems[0].MenuItemId.Should().Be(TestMenuItemId);
        result.LineItems[0].MenuItemName.Should().Be("Margherita Pizza");
        result.LineItems[0].Quantity.Should().Be(2);
        result.LineItems[0].UnitPrice.Should().Be(8.99m);

        result.LineItems[1].MenuItemName.Should().Be("Garlic Bread");
        result.LineItems[1].Quantity.Should().Be(1);
        result.LineItems[1].UnitPrice.Should().Be(3.50m);
    }

    /// <summary>
    /// AC: ORD-003-AC-01
    /// The response includes delivery address and order totals mapped correctly.
    /// </summary>
    [Fact]
    public async Task GetOrder_WhenOrderExists_ReturnsDeliveryAddressAndTotals()
    {
        // Arrange
        var order = CreateTestOrder();
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderQuery { OrderId = order.Id };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Delivery address
        result.DeliveryAddress.Should().NotBeNull();
        result.DeliveryAddress!.Street.Should().Be("123 High Street");
        result.DeliveryAddress.City.Should().Be("London");
        result.DeliveryAddress.Postcode.Should().Be("SW1A 1AA");
        result.DeliveryAddress.Latitude.Should().Be(51.5074);
        result.DeliveryAddress.Longitude.Should().Be(-0.1278);

        // Assert - Order totals
        result.OrderTotal.Should().NotBeNull();
        result.OrderTotal!.Subtotal.Should().Be(21.48m);
        result.OrderTotal.DeliveryFee.Should().Be(2.99m);
        result.OrderTotal.Total.Should().Be(24.47m);
    }

    /// <summary>
    /// AC: ORD-003-AC-01
    /// The response includes all state transition timestamps. For a freshly placed order,
    /// CreatedAt and PlacedAt are set; all other timestamps are null.
    /// </summary>
    [Fact]
    public async Task GetOrder_WhenOrderExists_ReturnsTimestampsCorrectly()
    {
        // Arrange
        var order = CreateTestOrder();
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderQuery { OrderId = order.Id };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Timestamps.Should().NotBeNull();
        result.Timestamps.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.Timestamps.PlacedAt.Should().NotBeNull();
        result.Timestamps.PlacedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        // All other timestamps should be null for a freshly placed order
        result.Timestamps.AcceptedAt.Should().BeNull();
        result.Timestamps.PreparingAt.Should().BeNull();
        result.Timestamps.ReadyForPickupAt.Should().BeNull();
        result.Timestamps.InDeliveryAt.Should().BeNull();
        result.Timestamps.DeliveredAt.Should().BeNull();
        result.Timestamps.RejectedAt.Should().BeNull();
        result.Timestamps.CancelledAt.Should().BeNull();
    }

    #endregion

    #region Error Path Tests

    /// <summary>
    /// AC: ORD-003-ERR-01
    /// When the order does not exist in the repository, the handler throws OrderNotFoundException.
    /// This maps to HTTP 404 at the function level.
    /// </summary>
    [Fact]
    public async Task GetOrder_WhenOrderNotFound_ThrowsOrderNotFoundException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var query = new GetOrderQuery { OrderId = orderId };

        // Act
        var act = async () => await handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OrderNotFoundException>()
            .Where(ex => ex.OrderId == orderId)
            .Where(ex => ex.ErrorCode == "ORDER_NOT_FOUND");
    }

    #endregion
}
