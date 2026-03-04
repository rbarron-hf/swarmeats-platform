using FluentAssertions;
using Moq;
using Restaurant.Domain.Aggregates;
using Restaurant.Domain.Exceptions;
using Restaurant.Domain.Queries;
using Restaurant.Domain.ValueObjects;
using Restaurant.Infrastructure.Repositories;
using Xunit;

namespace Restaurant.Tests.Unit;

/// <summary>
/// Unit tests for RST-001: Get Menu.
/// Tests the GetMenuQueryHandler against various scenarios.
/// </summary>
public class RST001Tests
{
    #region Test Helpers

    private static Menu CreateTestMenu()
    {
        var menu = new Menu(
            id: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
            restaurantName: "Test Restaurant",
            operatingHours: new OperatingHours("09:00", "22:00"));

        menu.MenuItems.Add(new MenuItem(
            menuItemId: Guid.NewGuid(),
            name: "Margherita Pizza",
            description: "Classic tomato and mozzarella",
            price: new Price(8.99m),
            category: "Mains",
            preparationTime: new PreparationTime(15),
            isAvailable: true));

        menu.MenuItems.Add(new MenuItem(
            menuItemId: Guid.NewGuid(),
            name: "Garlic Bread",
            description: "Toasted garlic bread with herbs",
            price: new Price(3.50m),
            category: "Starters",
            preparationTime: new PreparationTime(5),
            isAvailable: false));

        return menu;
    }

    #endregion

    /// <summary>
    /// AC: RST-001-AC-01
    /// When a valid restaurantId is provided and the menu exists,
    /// the full menu is returned with HTTP 200.
    /// </summary>
    [Fact]
    public async Task GetMenu_WhenMenuExists_ReturnsFullMenu()
    {
        // Arrange
        var menu = CreateTestMenu();
        var mockRepo = new Mock<IMenuRepository>();
        mockRepo.Setup(r => r.GetByRestaurantIdAsync(menu.RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(menu);

        var handler = new GetMenuQueryHandler(mockRepo.Object);
        var query = new GetMenuQuery { RestaurantId = menu.RestaurantId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RestaurantId.Should().Be(menu.RestaurantId);
        result.OperatingHours.OpeningTime.Should().Be("09:00");
        result.OperatingHours.ClosingTime.Should().Be("22:00");
        result.Items.Should().HaveCount(2);
    }

    /// <summary>
    /// AC: RST-001-AC-02
    /// Response includes menu items with correct fields.
    /// </summary>
    [Fact]
    public async Task GetMenu_ReturnsMenuItemsWithAllFields()
    {
        // Arrange
        var menu = CreateTestMenu();
        var mockRepo = new Mock<IMenuRepository>();
        mockRepo.Setup(r => r.GetByRestaurantIdAsync(menu.RestaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(menu);

        var handler = new GetMenuQueryHandler(mockRepo.Object);
        var query = new GetMenuQuery { RestaurantId = menu.RestaurantId };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var firstItem = result.Items.First(i => i.Name == "Margherita Pizza");
        firstItem.Description.Should().Be("Classic tomato and mozzarella");
        firstItem.Price.Amount.Should().Be(8.99m);
        firstItem.Price.Currency.Should().Be("GBP");
        firstItem.Category.Should().Be("Mains");
        firstItem.IsAvailable.Should().BeTrue();
        firstItem.PreparationTimeMinutes.Should().Be(15);
    }

    /// <summary>
    /// AC: RST-001-ERR-01
    /// When the restaurant does not exist, MenuNotFoundException is thrown.
    /// </summary>
    [Fact]
    public async Task GetMenu_WhenMenuNotFound_ThrowsMenuNotFoundException()
    {
        // Arrange
        var restaurantId = Guid.NewGuid();
        var mockRepo = new Mock<IMenuRepository>();
        mockRepo.Setup(r => r.GetByRestaurantIdAsync(restaurantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Menu?)null);

        var handler = new GetMenuQueryHandler(mockRepo.Object);
        var query = new GetMenuQuery { RestaurantId = restaurantId };

        // Act
        var act = async () => await handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<MenuNotFoundException>()
            .Where(ex => ex.RestaurantId == restaurantId)
            .Where(ex => ex.ErrorCode == "RESTAURANT_NOT_FOUND");
    }
}
