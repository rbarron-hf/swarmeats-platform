using FluentAssertions;
using Moq;
using Orders.Domain.Aggregates;
using Orders.Domain.Commands;
using Orders.Domain.Events;
using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;
using Orders.Infrastructure.Repositories;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-001: Place Order.
/// Tests business rule validation (ORD-R01 through ORD-R04), order total calculation (ORD-R06),
/// OrderPlaced domain event raising, and handler orchestration.
/// </summary>
public class ORD001Tests
{
    #region Test Helpers

    private static PlaceOrderCommand CreateValidCommand(
        Guid? customerId = null,
        Guid? restaurantId = null,
        List<PlaceOrderLineItem>? lineItems = null,
        PlaceOrderDeliveryAddress? deliveryAddress = null)
    {
        return new PlaceOrderCommand
        {
            CustomerId = customerId ?? Guid.NewGuid(),
            RestaurantId = restaurantId ?? Guid.NewGuid(),
            DeliveryAddress = deliveryAddress ?? new PlaceOrderDeliveryAddress
            {
                Street = "123 High Street",
                City = "London",
                Postcode = "SW1A 1AA",
                Latitude = 51.5074,
                Longitude = -0.1278
            },
            LineItems = lineItems ?? new List<PlaceOrderLineItem>
            {
                new()
                {
                    MenuItemId = Guid.NewGuid(),
                    MenuItemName = "Margherita Pizza",
                    Quantity = 2,
                    UnitPrice = 8.99m
                }
            }
        };
    }

    private static (PlaceOrderCommandHandler handler, Mock<IOrderRepository> repo) CreateHandler()
    {
        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var handler = new PlaceOrderCommandHandler(mockRepo.Object);
        return (handler, mockRepo);
    }

    #endregion

    #region Happy Path Tests

    /// <summary>
    /// AC: ORD-001-AC-01
    /// When a valid order is placed, a new Order aggregate is created with status Placed.
    /// HTTP 201 is returned with orderId, orderNumber, status, and calculated total.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WithValidData_ReturnsCreatedResultWithPlacedStatus()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().NotBeEmpty();
        result.OrderNumber.Should().StartWith("ORD-");
        result.Status.Should().Be("Placed");
        result.OrderTotal.Should().Be(17.98m + 2.99m); // 2 x 8.99 + 2.99 delivery
    }

    /// <summary>
    /// AC: ORD-001-AC-02
    /// Order total is calculated as subtotal + GBP 2.99 delivery fee (ORD-R06).
    /// </summary>
    [Fact]
    public async Task PlaceOrder_CalculatesOrderTotal_SubtotalPlusDeliveryFee()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand(lineItems: new List<PlaceOrderLineItem>
        {
            new() { MenuItemId = Guid.NewGuid(), MenuItemName = "Item A", Quantity = 1, UnitPrice = 10.00m },
            new() { MenuItemId = Guid.NewGuid(), MenuItemName = "Item B", Quantity = 2, UnitPrice = 5.00m }
        });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert - subtotal = 10.00 + (2 * 5.00) = 20.00; total = 20.00 + 2.99 = 22.99
        result.OrderTotal.Should().Be(22.99m);
    }

    /// <summary>
    /// AC: ORD-001-AC-03
    /// The order is persisted to the repository (Cosmos DB) via SaveAsync.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WithValidData_PersistsOrderViaRepository()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        repo.Verify(r => r.SaveAsync(It.Is<Order>(o =>
            o.Status == OrderStatus.Placed &&
            o.CustomerId == command.CustomerId &&
            o.RestaurantId == command.RestaurantId
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// AC: ORD-001-AC-04
    /// An OrderPlaced domain event is raised with correct payload when an order is created.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WithValidData_RaisesOrderPlacedDomainEvent()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var menuItemId = Guid.NewGuid();
        Order? savedOrder = null;

        var mockRepo = new Mock<IOrderRepository>();
        mockRepo.Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => savedOrder = o)
            .Returns(Task.CompletedTask);

        var handler = new PlaceOrderCommandHandler(mockRepo.Object);
        var command = CreateValidCommand(
            customerId: customerId,
            restaurantId: restaurantId,
            lineItems: new List<PlaceOrderLineItem>
            {
                new() { MenuItemId = menuItemId, MenuItemName = "Pizza", Quantity = 2, UnitPrice = 8.99m }
            });

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        savedOrder.Should().NotBeNull();
        savedOrder!.DomainEvents.Should().HaveCount(1);
        var domainEvent = savedOrder.DomainEvents[0].Should().BeOfType<OrderPlaced>().Subject;

        domainEvent.OrderId.Should().Be(savedOrder.Id);
        domainEvent.CustomerId.Should().Be(customerId);
        domainEvent.RestaurantId.Should().Be(restaurantId);
        domainEvent.LineItems.Should().HaveCount(1);
        domainEvent.LineItems[0].MenuItemId.Should().Be(menuItemId);
        domainEvent.LineItems[0].Quantity.Should().Be(2);
        domainEvent.LineItems[0].UnitPrice.Should().Be(8.99m);
        domainEvent.OrderTotal.Should().Be(20.97m);
        domainEvent.PlacedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        domainEvent.EventId.Should().NotBeEmpty();
    }

    /// <summary>
    /// AC: ORD-001-AC-05
    /// The order number is generated in format ORD-YYYYMMDD-sequential (ORD-R07).
    /// </summary>
    [Fact]
    public async Task PlaceOrder_GeneratesOrderNumber_InCorrectFormat()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.OrderNumber.Should().MatchRegex(@"^ORD-\d{8}-[A-Z0-9]{6}$");
    }

    #endregion

    #region Validation Error Tests

    /// <summary>
    /// AC: ORD-001-ERR-01
    /// When order subtotal is below GBP 10.00 (ORD-R01), InvalidOrderException
    /// is thrown with error code ORDER_MINIMUM_NOT_MET.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenSubtotalBelowMinimum_ThrowsInvalidOrderException()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand(lineItems: new List<PlaceOrderLineItem>
        {
            new() { MenuItemId = Guid.NewGuid(), MenuItemName = "Small Item", Quantity = 1, UnitPrice = 5.00m }
        });

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOrderException>()
            .Where(ex => ex.ErrorCode == "ORDER_MINIMUM_NOT_MET");
    }

    /// <summary>
    /// AC: ORD-001-ERR-02
    /// When more than 20 line items are provided (ORD-R02), InvalidOrderException
    /// is thrown with error code ORDER_TOO_MANY_ITEMS.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenTooManyItems_ThrowsInvalidOrderException()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var tooManyItems = Enumerable.Range(1, 21)
            .Select(i => new PlaceOrderLineItem
            {
                MenuItemId = Guid.NewGuid(),
                MenuItemName = $"Item {i}",
                Quantity = 1,
                UnitPrice = 1.00m
            }).ToList();
        var command = CreateValidCommand(lineItems: tooManyItems);

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOrderException>()
            .Where(ex => ex.ErrorCode == "ORDER_TOO_MANY_ITEMS");
    }

    /// <summary>
    /// AC: ORD-001-ERR-03
    /// When a line item has quantity less than 1, InvalidOrderException
    /// is thrown with error code ORDER_INVALID_LINE_ITEM.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenLineItemHasInvalidQuantity_ThrowsInvalidOrderException()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand(lineItems: new List<PlaceOrderLineItem>
        {
            new() { MenuItemId = Guid.NewGuid(), MenuItemName = "Bad Item", Quantity = 0, UnitPrice = 10.00m }
        });

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOrderException>()
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_LINE_ITEM");
    }

    /// <summary>
    /// AC: ORD-001-ERR-03
    /// When a line item has unit price of zero or negative, InvalidOrderException
    /// is thrown with error code ORDER_INVALID_LINE_ITEM.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenLineItemHasInvalidPrice_ThrowsInvalidOrderException()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand(lineItems: new List<PlaceOrderLineItem>
        {
            new() { MenuItemId = Guid.NewGuid(), MenuItemName = "Free Item", Quantity = 1, UnitPrice = 0m }
        });

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOrderException>()
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_LINE_ITEM");
    }

    /// <summary>
    /// AC: ORD-001-ERR-04
    /// When delivery address is missing latitude, InvalidOrderException
    /// is thrown with error code ORDER_INVALID_ADDRESS.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenAddressMissingLatitude_ThrowsInvalidOrderException()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand(deliveryAddress: new PlaceOrderDeliveryAddress
        {
            Street = "123 High Street",
            City = "London",
            Postcode = "SW1A 1AA",
            Latitude = null,
            Longitude = -0.1278
        });

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOrderException>()
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_ADDRESS");
    }

    /// <summary>
    /// AC: ORD-001-ERR-05
    /// When customerId is an empty GUID, InvalidOrderException
    /// is thrown with error code ORDER_INVALID_CUSTOMER.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenCustomerIdIsEmpty_ThrowsInvalidOrderException()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand(customerId: Guid.Empty);

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOrderException>()
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_CUSTOMER");
    }

    /// <summary>
    /// AC: ORD-001-ERR-06
    /// When restaurantId is an empty GUID, InvalidOrderException
    /// is thrown with error code ORDER_INVALID_RESTAURANT.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenRestaurantIdIsEmpty_ThrowsInvalidOrderException()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand(restaurantId: Guid.Empty);

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOrderException>()
            .Where(ex => ex.ErrorCode == "ORDER_INVALID_RESTAURANT");
    }

    /// <summary>
    /// AC: ORD-001-EDGE-01
    /// When the order subtotal is exactly GBP 10.00 (minimum threshold), the order
    /// should be accepted and processed successfully.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenSubtotalExactlyMinimum_Succeeds()
    {
        // Arrange
        var (handler, repo) = CreateHandler();
        var command = CreateValidCommand(lineItems: new List<PlaceOrderLineItem>
        {
            new() { MenuItemId = Guid.NewGuid(), MenuItemName = "Exact Min", Quantity = 1, UnitPrice = 10.00m }
        });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Placed");
        result.OrderTotal.Should().Be(12.99m); // 10.00 + 2.99
    }

    #endregion

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: ORD-001-AC-01
    /// The Order aggregate constructor sets the status to Placed and records
    /// both CreatedAt and PlacedAt timestamps.
    /// </summary>
    [Fact]
    public void OrderConstructor_SetsStatusToPlacedAndRecordsTimestamps()
    {
        // Arrange & Act
        var order = new Order(
            id: Guid.NewGuid(),
            orderNumber: "ORD-20260303-ABC123",
            customerId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            lineItems: new List<OrderLineItem>
            {
                new(Guid.NewGuid(), "Pizza", 1, 12.00m)
            },
            deliveryAddress: new DeliveryAddress("123 High St", "London", "SW1A 1AA", 51.5074, -0.1278),
            orderTotal: new OrderTotal(12.00m, 2.99m, 14.99m));

        // Assert
        order.Status.Should().Be(OrderStatus.Placed);
        order.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        order.PlacedAt.Should().NotBeNull();
        order.PlacedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: ORD-001-AC-04
    /// The Order aggregate constructor raises an OrderPlaced domain event with
    /// correct delivery address mapping.
    /// </summary>
    [Fact]
    public void OrderConstructor_RaisesOrderPlacedEvent_WithDeliveryAddress()
    {
        // Arrange & Act
        var order = new Order(
            id: Guid.NewGuid(),
            orderNumber: "ORD-20260303-ABC123",
            customerId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            lineItems: new List<OrderLineItem>
            {
                new(Guid.NewGuid(), "Pizza", 1, 12.00m)
            },
            deliveryAddress: new DeliveryAddress("42 Baker Street", "London", "NW1 6XE", 51.5237, -0.1585),
            orderTotal: new OrderTotal(12.00m, 2.99m, 14.99m));

        // Assert
        order.DomainEvents.Should().HaveCount(1);
        var evt = order.DomainEvents[0].Should().BeOfType<OrderPlaced>().Subject;
        evt.DeliveryAddress.Street.Should().Be("42 Baker Street");
        evt.DeliveryAddress.City.Should().Be("London");
        evt.DeliveryAddress.Postcode.Should().Be("NW1 6XE");
        evt.DeliveryAddress.Latitude.Should().Be(51.5237);
        evt.DeliveryAddress.Longitude.Should().Be(-0.1585);
    }

    #endregion
}
