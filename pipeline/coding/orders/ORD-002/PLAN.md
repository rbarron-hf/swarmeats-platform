# ORD-002 Cancel Order - Implementation Plan

## Story

**ID:** ORD-002
**Context:** Orders
**Title:** Cancel an order that has not yet been accepted
**User Story:** As a customer, I want to cancel my order while it is still in Placed status so that I am not charged and the restaurant does not prepare food I no longer want.

## Architecture Decisions

### Layer Separation (DDD)

The implementation follows strict DDD layering:

1. **Domain Layer** (`src/Orders/Domain/`) - Pure business logic, zero infrastructure references
2. **Infrastructure Layer** (`src/Orders/Infrastructure/`) - Cosmos DB persistence, outbox pattern
3. **Function Layer** (`src/Orders/Functions/`) - HTTP concerns only (routing, serialization, error mapping)

### Business Rule Enforcement

Business rule **ORD-R05** (cancellation only from Placed status) is enforced exclusively inside the `Order.Cancel()` aggregate method. The handler and function contain no business logic.

### Domain Event Pattern

The `OrderCancelled` domain event is raised inside the aggregate via `AddDomainEvent()`. The event is persisted to the outbox atomically with the aggregate state change using a Cosmos DB transactional batch in the repository.

### Error Handling Strategy

| Exception | HTTP Status | Error Code |
|-----------|------------|------------|
| `OrderNotFoundException` | 404 | `ORDER_NOT_FOUND` |
| `OrderCannotBeCancelledException` | 409 | `ORDER_CANNOT_CANCEL` |
| Unexpected exceptions | 500 | `INTERNAL_ERROR` |

## Files Produced

### Domain Model

| File | Purpose |
|------|---------|
| `src/Orders/Domain/ValueObjects/OrderStatus.cs` | Enum: Draft, Placed, Accepted, Preparing, ReadyForPickup, InDelivery, Delivered, Rejected, Cancelled |
| `src/Orders/Domain/Events/IDomainEvent.cs` | Marker interface with EventId and OccurredAt |
| `src/Orders/Domain/Events/OrderCancelled.cs` | Domain event: orderId, orderNumber, restaurantId, cancelledAt |
| `src/Orders/Domain/Exceptions/OrderNotFoundException.cs` | 404 exception with orderId |
| `src/Orders/Domain/Exceptions/OrderCannotBeCancelledException.cs` | 409 exception with orderId and currentStatus |
| `src/Orders/Domain/Aggregates/AggregateRoot.cs` | Base class with domain event collection and ETag |
| `src/Orders/Domain/Aggregates/Order.cs` | Aggregate root with Cancel() method, value objects |

### Commands

| File | Purpose |
|------|---------|
| `src/Orders/Domain/Commands/CancelOrderCommand.cs` | MediatR IRequest with OrderId; CancelOrderResult DTO |
| `src/Orders/Domain/Commands/CancelOrderCommandHandler.cs` | Loads order, delegates to Cancel(), saves |

### Infrastructure

| File | Purpose |
|------|---------|
| `src/Orders/Infrastructure/Repositories/IOrderRepository.cs` | Interface: GetByIdAsync, SaveAsync |
| `src/Orders/Infrastructure/Repositories/CosmosOrderRepository.cs` | Cosmos DB implementation with transactional batch outbox |

### Azure Function

| File | Purpose |
|------|---------|
| `src/Orders/Functions/CancelOrderFunction.cs` | HTTP POST trigger, isolated worker, MediatR dispatch, exception mapping |

### Tests

| File | Purpose |
|------|---------|
| `tests/Orders.Tests/Unit/ORD002Tests.cs` | 6 xUnit tests with Moq and FluentAssertions |

## Test Coverage

| Test Method | What It Verifies | AC Reference |
|-------------|-----------------|--------------|
| `CancelOrder_WhenStatusIsPlaced_TransitionsToCancelled` | Status changes from Placed to Cancelled | ORD-002-AC-01 |
| `CancelOrder_WhenStatusIsPlaced_RaisesOrderCancelledEvent` | OrderCancelled event with correct payload | ORD-002-AC-03 |
| `CancelOrder_WhenStatusIsAccepted_ThrowsOrderCannotBeCancelledException` | Guard rejects non-Placed status | ORD-002-ERR-02 |
| `CancelOrder_WhenStatusIsDelivered_ThrowsOrderCannotBeCancelledException` | Guard rejects terminal status | ORD-002-ERR-02 |
| `CancelOrder_WhenOrderNotFound_ThrowsOrderNotFoundException` | Missing order raises 404-mapped exception | ORD-002-ERR-01 |
| `CancelOrder_ReturnsUpdatedOrderWithCancelledStatus` | Handler returns correct result and calls SaveAsync | ORD-002-AC-02, AC-04 |

## Dependencies

- **NuGet Packages Required:**
  - `MediatR` - CQRS command/query dispatching
  - `Microsoft.Azure.Functions.Worker` - Azure Functions isolated worker model
  - `Microsoft.Azure.Functions.Worker.Extensions.Http` - HTTP trigger binding
  - `Microsoft.Azure.Cosmos` - Cosmos DB SDK
  - `xunit` - Test framework
  - `Moq` - Mocking framework
  - `FluentAssertions` - Assertion library

## Downstream Event Consumers

The `OrderCancelled` event published to `orders.cancelled` topic is consumed by:
- **Restaurant context:** Cancels pending RestaurantOrder if still in Pending status
- **Delivery context:** Cancels delivery if still in AwaitingDriver status
