using FluentAssertions;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Orders.Domain.Aggregates;
using Orders.Domain.Commands;
using Orders.Domain.Events;
using Orders.Domain.Exceptions;
using Orders.Domain.ValueObjects;
using Orders.Functions;
using Orders.Infrastructure.Repositories;
using System.Net;
using Xunit;

namespace Orders.Tests.Unit;

/// <summary>
/// Unit tests for ORD-002: Cancel Order.
/// Tests the Order aggregate Cancel() method, the CancelOrderCommandHandler, and the CancelOrderFunction.
/// </summary>
public class ORD002Tests
{
    #region Test Helpers

    /// <summary>
    /// Creates a test Order aggregate in the specified status.
    /// Uses reflection to set the Status property for testing non-Placed states.
    /// </summary>
    private static Order CreateTestOrder(OrderStatus status = OrderStatus.Placed)
    {
        var order = new Order(
            id: Guid.NewGuid(),
            orderNumber: "ORD-20260303-001",
            customerId: Guid.NewGuid(),
            restaurantId: Guid.NewGuid(),
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

        // If a different status is requested, use reflection to set it
        if (status != OrderStatus.Placed)
        {
            var statusProperty = typeof(Order).GetProperty(nameof(Order.Status));
            statusProperty!.SetValue(order, status);
        }

        return order;
    }

    #endregion

    #region Aggregate-Level Tests

    /// <summary>
    /// AC: ORD-002-AC-01
    /// When an order is in Placed status and Cancel() is called,
    /// the status transitions to Cancelled.
    /// </summary>
    [Fact]
    public void CancelOrder_WhenStatusIsPlaced_TransitionsToCancelled()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        order.Cancel();

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelledAt.Should().NotBeNull();
        order.CancelledAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: ORD-002-AC-03
    /// When an order in Placed status is cancelled, an OrderCancelled domain event
    /// is raised with the correct payload (orderId, orderNumber, restaurantId, cancelledAt).
    /// </summary>
    [Fact]
    public void CancelOrder_WhenStatusIsPlaced_RaisesOrderCancelledEvent()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);

        // Act
        order.Cancel();

        // Assert
        order.DomainEvents.Should().HaveCount(1);

        var domainEvent = order.DomainEvents[0];
        domainEvent.Should().BeOfType<OrderCancelled>();

        var cancelledEvent = (OrderCancelled)domainEvent;
        cancelledEvent.OrderId.Should().Be(order.Id);
        cancelledEvent.OrderNumber.Should().Be(order.OrderNumber);
        cancelledEvent.RestaurantId.Should().Be(order.RestaurantId);
        cancelledEvent.CancelledAt.Should().Be(order.CancelledAt!.Value);
        cancelledEvent.EventId.Should().NotBeEmpty();
        cancelledEvent.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// AC: ORD-002-ERR-02
    /// When an order is in Accepted status, Cancel() throws OrderCannotBeCancelledException.
    /// Business rule ORD-R05: cancellation only permitted from Placed status.
    /// </summary>
    [Fact]
    public void CancelOrder_WhenStatusIsAccepted_ThrowsOrderCannotBeCancelledException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Accepted);

        // Act
        var act = () => order.Cancel();

        // Assert
        act.Should().Throw<OrderCannotBeCancelledException>()
            .Where(ex => ex.OrderId == order.Id)
            .Where(ex => ex.CurrentStatus == OrderStatus.Accepted)
            .Where(ex => ex.ErrorCode == "ORDER_CANNOT_CANCEL");

        // Verify aggregate state is completely unmodified after failed cancellation
        order.Status.Should().Be(OrderStatus.Accepted);
        order.CancelledAt.Should().BeNull();
        order.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// AC: ORD-002-ERR-02
    /// When an order is in Delivered status, Cancel() throws OrderCannotBeCancelledException.
    /// Terminal states must not allow cancellation.
    /// </summary>
    [Fact]
    public void CancelOrder_WhenStatusIsDelivered_ThrowsOrderCannotBeCancelledException()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Delivered);

        // Act
        var act = () => order.Cancel();

        // Assert
        act.Should().Throw<OrderCannotBeCancelledException>()
            .Where(ex => ex.OrderId == order.Id)
            .Where(ex => ex.CurrentStatus == OrderStatus.Delivered)
            .Where(ex => ex.ErrorCode == "ORDER_CANNOT_CANCEL");

        // Verify aggregate state is completely unmodified after failed cancellation
        order.Status.Should().Be(OrderStatus.Delivered);
        order.CancelledAt.Should().BeNull();
        order.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Handler-Level Tests

    /// <summary>
    /// AC: ORD-002-ERR-01
    /// When the order does not exist in the repository, the handler throws OrderNotFoundException.
    /// This maps to HTTP 404 at the function level.
    /// </summary>
    [Fact]
    public async Task CancelOrder_WhenOrderNotFound_ThrowsOrderNotFoundException()
    {
        // Arrange
        var mockRepository = new Mock<IOrderRepository>();
        var orderId = Guid.NewGuid();

        mockRepository
            .Setup(r => r.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new CancelOrderCommandHandler(mockRepository.Object);
        var command = new CancelOrderCommand { OrderId = orderId };

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<OrderNotFoundException>()
            .Where(ex => ex.OrderId == orderId)
            .Where(ex => ex.ErrorCode == "ORDER_NOT_FOUND");
    }

    /// <summary>
    /// AC: ORD-002-AC-04
    /// When cancellation succeeds, the handler returns a CancelOrderResult with
    /// the order's updated status set to Cancelled, along with orderId, orderNumber, and cancelledAt.
    /// </summary>
    [Fact]
    public async Task CancelOrder_ReturnsUpdatedOrderWithCancelledStatus()
    {
        // Arrange
        var order = CreateTestOrder(OrderStatus.Placed);
        var mockRepository = new Mock<IOrderRepository>();

        mockRepository
            .Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        mockRepository
            .Setup(r => r.SaveAsync(order, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CancelOrderCommandHandler(mockRepository.Object);
        var command = new CancelOrderCommand { OrderId = order.Id };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().Be(order.Id);
        result.OrderNumber.Should().Be(order.OrderNumber);
        result.Status.Should().Be("Cancelled");
        result.CancelledAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));

        // Verify the repository was called to save
        mockRepository.Verify(r => r.SaveAsync(order, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
