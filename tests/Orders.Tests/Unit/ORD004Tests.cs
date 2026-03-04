using FluentAssertions;
using Moq;
using Orders.Domain.Aggregates;
using Orders.Domain.Queries;
using Orders.Domain.ValueObjects;
using Orders.Infrastructure.Repositories;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-004: Get Orders by Customer.
/// Tests the GetOrdersByCustomerQueryHandler mapping, pagination, and empty results.
/// </summary>
public class ORD004Tests
{
    #region Test Helpers

    private static readonly Guid TestCustomerId = Guid.NewGuid();
    private static readonly Guid TestRestaurantId = Guid.NewGuid();

    /// <summary>
    /// Creates a test Order aggregate with known data for assertion.
    /// </summary>
    private static Order CreateTestOrder(Guid? orderId = null, string orderNumber = "ORD-20260303-001")
    {
        return new Order(
            id: orderId ?? Guid.NewGuid(),
            orderNumber: orderNumber,
            customerId: TestCustomerId,
            restaurantId: TestRestaurantId,
            lineItems: new List<OrderLineItem>
            {
                new(
                    menuItemId: Guid.NewGuid(),
                    menuItemName: "Margherita Pizza",
                    quantity: 2,
                    unitPrice: 8.99m)
            },
            deliveryAddress: new DeliveryAddress("123 High Street", "London", "SW1A 1AA", 51.5074, -0.1278),
            orderTotal: new OrderTotal(17.98m, 2.99m, 20.97m));
    }

    private static (GetOrdersByCustomerQueryHandler handler, Mock<IOrderRepository> repo) CreateHandler()
    {
        var mockRepo = new Mock<IOrderRepository>();
        var handler = new GetOrdersByCustomerQueryHandler(mockRepo.Object);
        return (handler, mockRepo);
    }

    #endregion

    #region Happy Path Tests

    /// <summary>
    /// AC: ORD-004-AC-01
    /// When a customer has orders, the handler returns a list of order summaries
    /// with correct orderId, orderNumber, total, status, and createdAt.
    /// </summary>
    [Fact]
    public async Task GetOrdersByCustomer_WhenOrdersExist_ReturnsOrderSummaries()
    {
        // Arrange
        var order1 = CreateTestOrder(orderNumber: "ORD-20260303-001");
        var order2 = CreateTestOrder(orderNumber: "ORD-20260303-002");
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByCustomerIdAsync(
                TestCustomerId, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order> { order1, order2 }, (string?)null));

        var query = new GetOrdersByCustomerQuery { CustomerId = TestCustomerId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Orders.Should().HaveCount(2);

        result.Orders[0].OrderId.Should().Be(order1.Id);
        result.Orders[0].OrderNumber.Should().Be("ORD-20260303-001");
        result.Orders[0].Total.Should().Be(20.97m);
        result.Orders[0].Status.Should().Be("Placed");
        result.Orders[0].CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        result.Orders[1].OrderId.Should().Be(order2.Id);
        result.Orders[1].OrderNumber.Should().Be("ORD-20260303-002");
    }

    /// <summary>
    /// AC: ORD-004-AC-02
    /// When more results exist, the response includes a continuationToken for the next page.
    /// </summary>
    [Fact]
    public async Task GetOrdersByCustomer_WhenMoreResultsExist_ReturnsContinuationToken()
    {
        // Arrange
        var order = CreateTestOrder();
        var expectedToken = "some-continuation-token";
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByCustomerIdAsync(
                TestCustomerId, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order> { order }, expectedToken));

        var query = new GetOrdersByCustomerQuery { CustomerId = TestCustomerId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.ContinuationToken.Should().Be(expectedToken);
    }

    /// <summary>
    /// AC: ORD-004-AC-02
    /// When a continuation token is provided, it is passed through to the repository.
    /// </summary>
    [Fact]
    public async Task GetOrdersByCustomer_WithContinuationToken_PassesTokenToRepository()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var inputToken = "previous-page-token";

        repo.Setup(r => r.GetByCustomerIdAsync(
                TestCustomerId, inputToken, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), (string?)null));

        var query = new GetOrdersByCustomerQuery
        {
            CustomerId = TestCustomerId,
            ContinuationToken = inputToken
        };

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert
        repo.Verify(r => r.GetByCustomerIdAsync(
            TestCustomerId, inputToken, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// AC: ORD-004-AC-03
    /// When a customer has no orders, an empty list is returned with no continuation token.
    /// </summary>
    [Fact]
    public async Task GetOrdersByCustomer_WhenNoOrders_ReturnsEmptyList()
    {
        // Arrange
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByCustomerIdAsync(
                TestCustomerId, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), (string?)null));

        var query = new GetOrdersByCustomerQuery { CustomerId = TestCustomerId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Orders.Should().BeEmpty();
        result.ContinuationToken.Should().BeNull();
    }

    /// <summary>
    /// AC: ORD-004-AC-01
    /// When no more results exist, the continuationToken is null.
    /// </summary>
    [Fact]
    public async Task GetOrdersByCustomer_WhenNoMoreResults_ContinuationTokenIsNull()
    {
        // Arrange
        var order = CreateTestOrder();
        var (handler, repo) = CreateHandler();

        repo.Setup(r => r.GetByCustomerIdAsync(
                TestCustomerId, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order> { order }, (string?)null));

        var query = new GetOrdersByCustomerQuery { CustomerId = TestCustomerId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.ContinuationToken.Should().BeNull();
    }

    #endregion
}
