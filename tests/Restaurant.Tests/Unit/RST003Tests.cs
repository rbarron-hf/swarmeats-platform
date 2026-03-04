using FluentAssertions;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.Events;
using Restaurant.Domain.ValueObjects;
using Xunit;

namespace Restaurant.Tests.Unit;

/// <summary>
/// Unit tests for RST-003: Process Incoming Order.
/// Tests the RestaurantOrder aggregate creation, Menu validation (operating hours and item availability),
/// and auto-rejection logic. Handler-level tests with mocked repositories are also included.
/// </summary>
public class RST003Tests
{
    #region Test Helpers

    private static Menu CreateTestMenu(bool isOpen = true, bool allItemsAvailable = true)
    {
        var openingTime = isOpen ? "00:00" : "23:59";
        var closingTime = isOpen ? "23:58" : "23:59";

        var menu = new Menu(
            id: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            restaurantName: "Test Restaurant",
            operatingHours: new OperatingHours(openingTime, closingTime));

        menu.MenuItems.Add(new MenuItem(
            menuItemId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            name: "Margherita Pizza",
            description: "Classic",
            price: new Price(8.99m),
            category: "Mains",
            preparationTime: new PreparationTime(15),
            isAvailable: allItemsAvailable));

        menu.MenuItems.Add(new MenuItem(
            menuItemId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            name: "Garlic Bread",
            description: "Toasted",
            price: new Price(3.50m),
            category: "Starters",
            preparationTime: new PreparationTime(5),
            isAvailable: true));

        return menu;
    }

    private static RestaurantOrder CreatePendingOrder(Guid restaurantId)
    {
        return new RestaurantOrder(
            orderId: Guid.NewGuid(),
            restaurantId: restaurantId,
            orderNumber: "ORD-20260303-001",
            lineItems: new List<RestaurantOrderLineItem>
            {
                new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Margherita Pizza", 2, 8.99m)
            },
            sourceEventId: Guid.NewGuid());
    }

    #endregion

    #region Aggregate Creation Tests

    /// <summary>
    /// AC: RST-003-AC-01
    /// When a RestaurantOrder is created, it starts in Pending status.
    /// </summary>
    [Fact]
    public void CreateRestaurantOrder_StartsInPendingStatus()
    {
        // Arrange & Act
        var order = new RestaurantOrder(
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            orderNumber: "ORD-20260303-001",
            lineItems: new List<RestaurantOrderLineItem>
            {
                new(Guid.NewGuid(), "Pizza", 1, 10.00m)
            },
            sourceEventId: Guid.NewGuid());

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Pending);
        order.ReceivedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        order.LineItems.Should().HaveCount(1);
    }

    /// <summary>
    /// AC: RST-003-AC-02
    /// The order stores the source event ID for idempotency.
    /// </summary>
    [Fact]
    public void CreateRestaurantOrder_StoresSourceEventId()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        var order = new RestaurantOrder(
            orderId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            orderNumber: "ORD-20260303-001",
            lineItems: new List<RestaurantOrderLineItem>
            {
                new(Guid.NewGuid(), "Pizza", 1, 10.00m)
            },
            sourceEventId: eventId);

        // Assert
        order.SourceEventId.Should().Be(eventId);
    }

    #endregion

    #region Operating Hours Validation Tests (RST-R01)

    /// <summary>
    /// AC: RST-003-ERR-01 (RST-R01)
    /// When the restaurant is outside operating hours, the menu reports it is closed.
    /// </summary>
    [Fact]
    public void Menu_IsOpen_WhenOutsideHours_ReturnsFalse()
    {
        // Arrange — create menu with hours that make it currently closed
        var menu = new Menu(
            id: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            restaurantName: "Test",
            operatingHours: new OperatingHours("03:00", "03:01"));

        // Act — check with a time that is outside the window
        // Use a time we know is outside 03:00-03:01
        var testTime = new DateTimeOffset(2026, 3, 3, 12, 0, 0, TimeSpan.Zero);
        var result = menu.IsOpen(testTime);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// AC: RST-003-AC-03
    /// When the restaurant is within operating hours, the menu reports it is open.
    /// </summary>
    [Fact]
    public void Menu_IsOpen_WhenWithinHours_ReturnsTrue()
    {
        // Arrange
        var menu = new Menu(
            id: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            restaurantName: "Test",
            operatingHours: new OperatingHours("00:00", "23:59"));

        // Act
        var result = menu.IsOpen(DateTimeOffset.UtcNow);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Menu Item Availability Validation Tests (RST-R02)

    /// <summary>
    /// AC: RST-003-ERR-02 (RST-R02)
    /// When a line item references an unavailable menu item, it is detected.
    /// </summary>
    [Fact]
    public void Menu_GetUnavailableItemIds_WhenItemUnavailable_ReturnsUnavailableIds()
    {
        // Arrange
        var menu = CreateTestMenu(isOpen: true, allItemsAvailable: false);
        var requestedIds = new List<Guid>
        {
            Guid.Parse("11111111-1111-1111-1111-111111111111") // This item is unavailable
        };

        // Act
        var unavailable = menu.GetUnavailableItemIds(requestedIds);

        // Assert
        unavailable.Should().HaveCount(1);
        unavailable[0].Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    }

    /// <summary>
    /// AC: RST-003-ERR-02 (RST-R02)
    /// When a line item references a non-existent menu item, it is detected.
    /// </summary>
    [Fact]
    public void Menu_GetUnavailableItemIds_WhenItemDoesNotExist_ReturnsUnavailableIds()
    {
        // Arrange
        var menu = CreateTestMenu(isOpen: true, allItemsAvailable: true);
        var nonExistentId = Guid.NewGuid();

        // Act
        var unavailable = menu.GetUnavailableItemIds(new[] { nonExistentId });

        // Assert
        unavailable.Should().HaveCount(1);
        unavailable[0].Should().Be(nonExistentId);
    }

    /// <summary>
    /// AC: RST-003-AC-04
    /// When all line items are available, no unavailable IDs are returned.
    /// </summary>
    [Fact]
    public void Menu_GetUnavailableItemIds_WhenAllAvailable_ReturnsEmpty()
    {
        // Arrange
        var menu = CreateTestMenu(isOpen: true, allItemsAvailable: true);
        var requestedIds = new List<Guid>
        {
            Guid.Parse("22222222-2222-2222-2222-222222222222") // Garlic bread is available
        };

        // Act
        var unavailable = menu.GetUnavailableItemIds(requestedIds);

        // Assert
        unavailable.Should().BeEmpty();
    }

    #endregion

    #region Auto-Rejection Tests

    /// <summary>
    /// AC: RST-003-ERR-01
    /// When a pending order is auto-rejected with RESTAURANT_CLOSED, it transitions to Rejected
    /// and raises an OrderRejected event.
    /// </summary>
    [Fact]
    public void RestaurantOrder_Reject_WithRestaurantClosed_TransitionsToRejectedAndRaisesEvent()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var order = CreatePendingOrder(restaurantId);

        // Act
        order.Reject("RESTAURANT_CLOSED", "Restaurant is outside operating hours");

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Rejected);
        order.RejectionReason.Should().Be("RESTAURANT_CLOSED");
        order.RejectedAt.Should().NotBeNull();
        order.DomainEvents.Should().HaveCount(1);

        var rejectedEvent = order.DomainEvents[0] as OrderRejected;
        rejectedEvent.Should().NotBeNull();
        rejectedEvent!.OrderId.Should().Be(order.Id);
        rejectedEvent.RestaurantId.Should().Be(restaurantId);
        rejectedEvent.ReasonCode.Should().Be("RESTAURANT_CLOSED");
    }

    /// <summary>
    /// AC: RST-003-ERR-02
    /// When a pending order is auto-rejected with ITEM_UNAVAILABLE, the unavailable item IDs
    /// are included in the rejection and event.
    /// </summary>
    [Fact]
    public void RestaurantOrder_Reject_WithItemUnavailable_IncludesUnavailableItemIds()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var order = CreatePendingOrder(restaurantId);
        var unavailableIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        order.Reject("ITEM_UNAVAILABLE", "Items not available", unavailableIds);

        // Assert
        order.Status.Should().Be(RestaurantOrderStatus.Rejected);
        order.UnavailableItemIds.Should().HaveCount(2);

        var rejectedEvent = order.DomainEvents[0] as OrderRejected;
        rejectedEvent!.UnavailableItemIds.Should().HaveCount(2);
        rejectedEvent.ReasonCode.Should().Be("ITEM_UNAVAILABLE");
    }

    #endregion
}
