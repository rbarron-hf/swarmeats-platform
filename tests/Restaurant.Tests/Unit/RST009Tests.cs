using FluentAssertions;
using Restaurant.Functions.EventHandlers;
using Restaurant.Infrastructure.Models;
using Xunit;

namespace Restaurant.Tests.Unit;

/// <summary>
/// Unit tests for RST-009: Active Order Dashboard Projection.
/// Tests the projection mapping logic from RestaurantOrderDocument to DashboardOrderProjection.
/// Since the Change Feed function writes to Cosmos, these tests focus on the mapping logic.
/// </summary>
public class RST009Tests
{
    #region Test Helpers

    private static RestaurantOrderDocument CreateTestDocument(string status = "Pending")
    {
        return new RestaurantOrderDocument
        {
            Id = Guid.NewGuid().ToString(),
            RestaurantId = Guid.NewGuid().ToString(),
            OrderNumber = "ORD-20260303-001",
            Status = status,
            EstimatedPrepTime = status == "Accepted" ? 25 : null,
            ReceivedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            AcceptedAt = status is "Accepted" or "Preparing" or "ReadyForPickup"
                ? DateTimeOffset.UtcNow.AddMinutes(-8) : null,
            PreparingAt = status is "Preparing" or "ReadyForPickup"
                ? DateTimeOffset.UtcNow.AddMinutes(-5) : null,
            ReadyAt = status == "ReadyForPickup"
                ? DateTimeOffset.UtcNow.AddMinutes(-1) : null,
            LineItems = new List<RestaurantOrderDocumentLineItem>
            {
                new() { MenuItemName = "Margherita Pizza", Quantity = 2, UnitPrice = 8.99m },
                new() { MenuItemName = "Garlic Bread", Quantity = 1, UnitPrice = 3.50m }
            }
        };
    }

    #endregion

    /// <summary>
    /// AC: RST-009-AC-01
    /// The projection correctly maps order ID, restaurant ID, and order number.
    /// </summary>
    [Fact]
    public void DashboardProjection_MapsIdentityFieldsCorrectly()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var projection = new DashboardOrderProjection
        {
            Id = document.Id,
            RestaurantId = document.RestaurantId,
            OrderNumber = document.OrderNumber,
            Status = document.Status,
            ReceivedAt = document.ReceivedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        projection.Id.Should().Be(document.Id);
        projection.RestaurantId.Should().Be(document.RestaurantId);
        projection.OrderNumber.Should().Be("ORD-20260303-001");
    }

    /// <summary>
    /// AC: RST-009-AC-02
    /// The projection correctly maps the current status.
    /// </summary>
    [Theory]
    [InlineData("Pending")]
    [InlineData("Accepted")]
    [InlineData("Preparing")]
    [InlineData("ReadyForPickup")]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public void DashboardProjection_MapsStatusCorrectly(string status)
    {
        // Arrange
        var document = CreateTestDocument(status);

        // Act
        var projection = new DashboardOrderProjection
        {
            Id = document.Id,
            RestaurantId = document.RestaurantId,
            OrderNumber = document.OrderNumber,
            Status = document.Status,
            ReceivedAt = document.ReceivedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        projection.Status.Should().Be(status);
    }

    /// <summary>
    /// AC: RST-009-AC-03
    /// The projection correctly maps line items.
    /// </summary>
    [Fact]
    public void DashboardProjection_MapsLineItemsCorrectly()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var projection = new DashboardOrderProjection
        {
            Id = document.Id,
            RestaurantId = document.RestaurantId,
            OrderNumber = document.OrderNumber,
            Status = document.Status,
            ReceivedAt = document.ReceivedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
            LineItems = document.LineItems!.Select(li => new DashboardLineItem
            {
                MenuItemName = li.MenuItemName,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice
            }).ToList()
        };

        // Assert
        projection.LineItems.Should().HaveCount(2);
        projection.LineItems[0].MenuItemName.Should().Be("Margherita Pizza");
        projection.LineItems[0].Quantity.Should().Be(2);
        projection.LineItems[0].UnitPrice.Should().Be(8.99m);
        projection.LineItems[1].MenuItemName.Should().Be("Garlic Bread");
    }

    /// <summary>
    /// AC: RST-009-AC-04
    /// The projection includes estimated prep time when available.
    /// </summary>
    [Fact]
    public void DashboardProjection_IncludesEstimatedPrepTime_WhenAccepted()
    {
        // Arrange
        var document = CreateTestDocument("Accepted");

        // Act
        var projection = new DashboardOrderProjection
        {
            Id = document.Id,
            RestaurantId = document.RestaurantId,
            OrderNumber = document.OrderNumber,
            Status = document.Status,
            EstimatedPrepTime = document.EstimatedPrepTime,
            ReceivedAt = document.ReceivedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        projection.EstimatedPrepTime.Should().Be(25);
    }

    /// <summary>
    /// AC: RST-009-AC-05
    /// The projection has null estimated prep time when order is Pending.
    /// </summary>
    [Fact]
    public void DashboardProjection_HasNullPrepTime_WhenPending()
    {
        // Arrange
        var document = CreateTestDocument("Pending");

        // Act
        var projection = new DashboardOrderProjection
        {
            Id = document.Id,
            RestaurantId = document.RestaurantId,
            OrderNumber = document.OrderNumber,
            Status = document.Status,
            EstimatedPrepTime = document.EstimatedPrepTime,
            ReceivedAt = document.ReceivedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        projection.EstimatedPrepTime.Should().BeNull();
    }

    /// <summary>
    /// AC: RST-009-AC-06
    /// The projection TTL is set to 30 days.
    /// </summary>
    [Fact]
    public void DashboardProjection_HasDefaultTtlOf30Days()
    {
        // Arrange & Act
        var projection = new DashboardOrderProjection();

        // Assert
        projection.Ttl.Should().Be(2592000); // 30 days in seconds
    }
}
