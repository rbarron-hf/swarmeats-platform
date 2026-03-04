# SwarmEats Architecture Guide

## 1. System Overview

SwarmEats is a food ordering platform built on Domain-Driven Design principles, deployed as Azure Functions (isolated worker model, .NET 8). The system decomposes into three bounded contexts that communicate asynchronously via Azure Service Bus topics and persist state in Azure Cosmos DB.

### Bounded Contexts

| Context | Responsibility | Aggregates |
|---------|---------------|------------|
| **Orders** | Customer-facing order lifecycle from placement through delivery confirmation | Order |
| **Restaurant** | Restaurant-side order processing, menu management, kitchen workflow | Menu, RestaurantOrder |
| **Delivery** | Driver assignment, location tracking, pickup/delivery confirmation | Delivery |

### Technology Stack

| Component | Technology |
|-----------|-----------|
| Compute | Azure Functions (isolated worker, .NET 8) |
| Database | Azure Cosmos DB (NoSQL, per-context containers) |
| Messaging | Azure Service Bus (topics + subscriptions) |
| Hosting | Azure Container Apps (KEDA queue-based scaling) |
| Orchestration | Azure Durable Functions (pipeline orchestrator) |
| IaC | Bicep (modular templates) |
| CQRS | MediatR (command/query dispatch) |
| Testing | xUnit + Moq + FluentAssertions |

---

## 2. Domain Model

### 2.1 Orders Context

**Order Aggregate** owns the full order lifecycle across 9 states:

```
Draft -> Placed -> Accepted -> Preparing -> ReadyForPickup -> InDelivery -> Delivered
                -> Rejected
                -> Cancelled
```

State transitions are triggered by a mix of HTTP commands (PlaceOrder, CancelOrder) and Service Bus events consumed from other contexts (OrderAccepted, OrderRejected, OrderReadyForPickup, DriverAssigned, DeliveryCompleted).

**Business Rules:**

| Rule | Description | Enforcement |
|------|-------------|-------------|
| ORD-R01 | Minimum order subtotal 10.00 GBP | PlaceOrderCommandHandler |
| ORD-R02 | Maximum 20 line items per order | PlaceOrderCommandHandler |
| ORD-R03 | Delivery address latitude/longitude required | PlaceOrderCommandHandler |
| ORD-R04 | Line items must reference valid menu item IDs | PlaceOrderCommandHandler |
| ORD-R05 | Cancellation only permitted from Placed status | Order.Cancel() |
| ORD-R06 | Order number format: ORD-YYYYMMDD-{sequential} | PlaceOrderCommandHandler |
| ORD-R07 | Delivery fee fixed at 2.99 GBP | PlaceOrderCommandHandler |

**Value Objects:** OrderStatus (enum), OrderLineItem (record), DeliveryAddress (record), OrderTotal (record)

**Domain Events Raised:** OrderPlaced, OrderCancelled

**Domain Events Consumed:** OrderAccepted, OrderRejected, OrderReadyForPickup, DriverAssigned, DeliveryCompleted

### 2.2 Restaurant Context

**Menu Aggregate** holds the restaurant's menu items and operating hours. Used for validation when processing incoming orders.

**RestaurantOrder Aggregate** tracks the restaurant-side view of an order through its kitchen lifecycle:

```
Pending -> Accepted -> Preparing -> ReadyForPickup
        -> Rejected
        -> Cancelled
```

**Business Rules:**

| Rule | Description | Enforcement |
|------|-------------|-------------|
| RST-R01 | Restaurant must be within operating hours | Menu.IsOpen() in HandleOrderPlacedFunction |
| RST-R02 | All line items must reference available menu items | Menu.GetUnavailableItemIds() in HandleOrderPlacedFunction |
| RST-R03 | Estimated prep time must be 5-90 minutes | RestaurantOrder.Accept() |
| RST-R04 | Rejection reason from allowed set | RestaurantOrder.Reject() |
| RST-R05 | Orders can only be rejected from Pending status | RestaurantOrder.Reject() |
| RST-R06 | Menu item prices > 0, prep time >= 1 minute | Price and PreparationTime constructors |

**Value Objects:** RestaurantOrderStatus (enum), Price (record), PreparationTime (record), OperatingHours (record)

**Domain Events Raised:** OrderAccepted, OrderRejected, OrderReadyForPickup

**Domain Events Consumed:** OrderPlaced, OrderCancelled

### 2.3 Delivery Context

**Delivery Aggregate** manages the driver assignment and delivery lifecycle:

```
AwaitingDriver -> DriverAssigned -> PickedUp -> Delivered
                                              -> Cancelled
```

**Business Rules:**

| Rule | Description | Enforcement |
|------|-------------|-------------|
| DLV-R01 | Driver must be available (not on active delivery) | AssignDriverCommandHandler via IDriverRepository |
| DLV-R02 | Driver must be within 5km of restaurant (Haversine) | DeliveryAggregate.AssignDriver() |
| DLV-R03 | Maximum 45-minute total delivery time SLA | DeliveryAggregate.Complete() (slaBreached flag) |
| DLV-R04 | Customer notified on driver assignment | DriverAssigned domain event |
| DLV-R05 | Unassigned deliveries > 30s trigger monitoring alert | UnassignedDeliveryMonitorFunction |

**Value Objects:** DeliveryStatus (enum), Location (record), DriverLocation (record), Route (record), EstimatedArrival (record), GeoCalculations (static Haversine helper)

**Domain Events Raised:** DriverAssigned, DeliveryCompleted

**Domain Events Consumed:** OrderReadyForPickup, OrderCancelled

---

## 3. Cross-Context Event Flows

All inter-context communication is asynchronous via Azure Service Bus topics. Each context subscribes only to the events it needs.

### 3.1 Service Bus Topics

| Topic | Publisher | Subscribers |
|-------|-----------|-------------|
| `orders.placed` | Orders (ORD-001) | Restaurant (RST-003) |
| `orders.cancelled` | Orders (ORD-002) | Restaurant (RST-008), Delivery (DLV-009) |
| `restaurant.order-accepted` | Restaurant (RST-004) | Orders (ORD-005) |
| `restaurant.order-rejected` | Restaurant (RST-003, RST-005) | Orders (ORD-006) |
| `restaurant.order-ready` | Restaurant (RST-007) | Orders (ORD-007), Delivery (DLV-001) |
| `delivery.driver-assigned` | Delivery (DLV-002) | Orders (ORD-008) |
| `delivery.completed` | Delivery (DLV-005) | Orders (ORD-009) |

### 3.2 End-to-End Order Flow

```
Customer places order (ORD-001)
  -> OrderPlaced event published to orders.placed
  -> Restaurant receives event (RST-003)
     -> Validates operating hours (RST-R01) + menu availability (RST-R02)
     -> Creates RestaurantOrder in Pending status
     -> OR auto-rejects if validation fails

Restaurant accepts order (RST-004)
  -> OrderAccepted event published to restaurant.order-accepted
  -> Orders context transitions Order to Accepted (ORD-005)

Restaurant marks preparing (RST-006)
  -> RestaurantOrder transitions to Preparing

Restaurant marks ready (RST-007)
  -> OrderReadyForPickup event published to restaurant.order-ready
  -> Orders context transitions Order to ReadyForPickup (ORD-007)
  -> Delivery context creates Delivery in AwaitingDriver (DLV-001)

Driver is assigned (DLV-002)
  -> Validates proximity (DLV-R02) + availability (DLV-R01)
  -> DriverAssigned event published to delivery.driver-assigned
  -> Orders context transitions Order to InDelivery (ORD-008)

Driver confirms pickup (DLV-004)
  -> Delivery transitions to PickedUp

Driver completes delivery (DLV-005)
  -> DeliveryCompleted event published to delivery.completed
  -> Orders context transitions Order to Delivered (ORD-009)
```

### 3.3 Cancellation Flow

```
Customer cancels order (ORD-002, only from Placed status)
  -> OrderCancelled event published to orders.cancelled
  -> Restaurant cancels RestaurantOrder if still Pending (RST-008)
  -> Delivery cancels Delivery if still AwaitingDriver (DLV-009)
```

---

## 4. Infrastructure Architecture

### 4.1 Azure Cosmos DB

Each bounded context owns its own containers. All containers use the outbox pattern for reliable event publishing.

| Container | Context | Partition Key | Purpose |
|-----------|---------|---------------|---------|
| `orders` | Orders | `/customerId` | Order aggregate storage |
| `orders-outbox` | Orders | `/customerId` | Outbox for OrderPlaced/OrderCancelled events |
| `menus` | Restaurant | `/restaurantId` | Menu aggregate storage |
| `restaurant-orders` | Restaurant | `/restaurantId` | RestaurantOrder aggregate storage |
| `restaurant-orders-outbox` | Restaurant | `/restaurantId` | Outbox for OrderAccepted/Rejected/Ready events |
| `dashboard-orders` | Restaurant | `/restaurantId` | Change feed projection for active order dashboard |
| `deliveries` | Delivery | `/driverId` | Delivery aggregate storage |
| `deliveries-outbox` | Delivery | `/driverId` | Outbox for DriverAssigned/DeliveryCompleted events |

**Key Patterns:**
- **Transactional batch**: Aggregate upsert + outbox messages in same partition, same transaction
- **Optimistic concurrency**: ETag-based conflict detection on all writes
- **Change feed**: Restaurant context projects order changes to a dashboard read model (RST-009)
- **TTL**: Outbox messages expire after 7 days; dashboard projections after 30 days

### 4.2 Azure Functions

26 functions across 3 Function Apps, covering all 4 trigger types:

| Trigger Type | Count | Examples |
|-------------|-------|---------|
| HTTP | 15 | PlaceOrder, CancelOrder, GetOrder, AcceptOrder, AssignDriver |
| Service Bus | 9 | HandleOrderPlaced, HandleOrderAccepted, HandleDriverAssigned |
| Cosmos Change Feed | 1 | ActiveOrderDashboard |
| Timer | 1 | UnassignedDeliveryMonitor (every 30 seconds) |

All functions use the **isolated worker model** (`Microsoft.Azure.Functions.Worker`).

### 4.3 Azure Service Bus

- 7 topics with per-context subscriptions
- Dead-letter queues for poison messages
- Service Bus functions handle idempotency by checking current aggregate state before applying transitions
- Invalid state transitions are logged as warnings and messages are completed (not dead-lettered) to prevent retry loops

---

## 5. DDD Patterns

### 5.1 Aggregate Design

All business logic lives inside aggregate roots. Command handlers follow the **load-delegate-save** pattern:

```csharp
public async Task<Result> Handle(Command request, CancellationToken ct)
{
    var aggregate = await _repository.GetByIdAsync(request.Id, ct);
    if (aggregate is null) throw new NotFoundException(request.Id);

    aggregate.DoSomething(request.Param);  // All rules enforced here

    await _repository.SaveAsync(aggregate, ct);
    return new Result { ... };
}
```

Handlers contain **zero business logic** -- they only orchestrate persistence and throw if the aggregate is not found.

### 5.2 Domain Events

Events are raised inside the aggregate via `AddDomainEvent()` and persisted atomically alongside the aggregate state using the outbox pattern. A background processor (Cosmos Change Feed) reads unprocessed outbox messages and publishes them to Service Bus.

```csharp
// Inside aggregate method
public void Cancel()
{
    if (Status != OrderStatus.Placed)
        throw new OrderCannotBeCancelledException(Id, Status);

    Status = OrderStatus.Cancelled;
    CancelledAt = DateTimeOffset.UtcNow;

    AddDomainEvent(new OrderCancelled
    {
        OrderId = Id,
        OrderNumber = OrderNumber,
        RestaurantId = RestaurantId,
        CancelledAt = CancelledAt.Value
    });
}
```

### 5.3 Value Objects

All value objects are immutable C# records with validation in constructors:

```csharp
public record OrderLineItem
{
    public Guid MenuItemId { get; init; }
    public string MenuItemName { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }

    public OrderLineItem(Guid menuItemId, string menuItemName, int quantity, decimal unitPrice)
    {
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPrice <= 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        // ...
    }

    private OrderLineItem() { } // Cosmos deserialization
}
```

### 5.4 CQRS

Commands and queries are dispatched via MediatR. Commands mutate state and may raise events. Queries are read-only and produce no side effects.

| Type | Base Interface | Returns |
|------|---------------|---------|
| Command | `IRequest<TResult>` | Result DTO |
| Query | `IRequest<TResponse>` | Response DTO |

### 5.5 Anti-Corruption Layer

Each context defines its own **consumed event DTOs** in a `Domain/Events/Consumed/` namespace. These DTOs map the external event schema to internal domain concepts, preventing upstream schema changes from leaking into the domain model.

---

## 6. Azure Function Endpoints

### 6.1 Orders Context

| Method | Route | Function | Story |
|--------|-------|----------|-------|
| POST | `/orders` | PlaceOrderFunction | ORD-001 |
| POST | `/orders/{orderId}/cancel` | CancelOrderFunction | ORD-002 |
| GET | `/orders/{orderId}` | GetOrderFunction | ORD-003 |
| GET | `/orders?customerId={id}` | GetOrdersByCustomerFunction | ORD-004 |

### 6.2 Restaurant Context

| Method | Route | Function | Story |
|--------|-------|----------|-------|
| GET | `/restaurants/{id}/menu` | GetMenuFunction | RST-001 |
| GET | `/restaurants/{id}/orders?status={s}` | GetActiveOrdersFunction | RST-002 |
| POST | `/restaurants/{id}/orders/{orderId}/accept` | AcceptOrderFunction | RST-004 |
| POST | `/restaurants/{id}/orders/{orderId}/reject` | RejectOrderFunction | RST-005 |
| PUT | `/restaurants/{id}/orders/{orderId}/status` | UpdatePreparationStatusFunction | RST-006/007 |

### 6.3 Delivery Context

| Method | Route | Function | Story |
|--------|-------|----------|-------|
| POST | `/deliveries/{id}/assign` | AssignDriverFunction | DLV-002 |
| PUT | `/deliveries/{id}/location` | UpdateDriverLocationFunction | DLV-003 |
| POST | `/deliveries/{id}/pickup` | ConfirmPickupFunction | DLV-004 |
| POST | `/deliveries/{id}/complete` | CompleteDeliveryFunction | DLV-005 |
| GET | `/deliveries/{id}` | GetDeliveryStatusFunction | DLV-006 |
| GET | `/deliveries/{id}/eta` | GetEstimatedArrivalFunction | DLV-007 |

---

## 7. Project Structure

```
src/
├── Orders/
│   ├── Domain/
│   │   ├── Aggregates/        # Order, AggregateRoot<T>
│   │   ├── Commands/          # PlaceOrder, CancelOrder (command + handler pairs)
│   │   ├── Events/            # OrderPlaced, OrderCancelled
│   │   │   └── Consumed/      # OrderAcceptedEvent, DriverAssignedEvent, etc.
│   │   ├── Exceptions/        # OrderNotFoundException, InvalidOrderException, etc.
│   │   ├── Queries/           # GetOrder, GetOrdersByCustomer (query + handler pairs)
│   │   └── ValueObjects/      # OrderStatus, OrderLineItem, DeliveryAddress, OrderTotal
│   ├── Functions/
│   │   ├── EventHandlers/     # Service Bus trigger functions
│   │   └── Models/            # ErrorResponse DTO
│   └── Infrastructure/
│       ├── Models/            # OutboxMessage
│       └── Repositories/      # IOrderRepository, CosmosOrderRepository
│
├── Restaurant/
│   ├── Domain/
│   │   ├── Aggregates/        # Menu, RestaurantOrder, AggregateRoot<T>
│   │   ├── Commands/          # AcceptOrder, RejectOrder, MarkPreparing, MarkReadyForPickup
│   │   ├── Events/            # OrderAccepted, OrderRejected, OrderReadyForPickup
│   │   │   └── Consumed/      # OrderPlacedEvent, OrderCancelledEvent
│   │   ├── Exceptions/        # RestaurantOrderNotFoundException, RestaurantClosedException, etc.
│   │   ├── Queries/           # GetMenu, GetActiveOrders
│   │   └── ValueObjects/      # RestaurantOrderStatus, Price, PreparationTime, OperatingHours
│   ├── Functions/
│   │   ├── EventHandlers/     # HandleOrderPlaced, HandleOrderCancelled, ActiveOrderDashboard
│   │   └── Models/            # ErrorResponse
│   └── Infrastructure/
│       ├── Models/            # OutboxMessage, DashboardOrderProjection
│       └── Repositories/      # IMenuRepository, IRestaurantOrderRepository + Cosmos impls
│
└── Delivery/
    ├── Domain/
    │   ├── Aggregates/        # DeliveryAggregate, AggregateRoot<T>
    │   ├── Commands/          # AssignDriver, UpdateDriverLocation, ConfirmPickup, CompleteDelivery
    │   ├── Events/            # DriverAssigned, DeliveryCompleted
    │   │   └── Consumed/      # OrderReadyForPickupEvent, OrderCancelledEvent
    │   ├── Exceptions/        # DeliveryNotFoundException, DriverTooFarException, etc.
    │   ├── Queries/           # GetDeliveryStatus, GetEstimatedArrival
    │   └── ValueObjects/      # DeliveryStatus, Location, DriverLocation, Route, GeoCalculations
    ├── Functions/
    │   ├── EventHandlers/     # HandleOrderReadyForPickup, HandleOrderCancelled, UnassignedMonitor
    │   └── Models/            # ErrorResponse
    └── Infrastructure/
        ├── Models/            # OutboxMessage
        └── Repositories/      # IDeliveryRepository, IDriverRepository + Cosmos impls

tests/
├── Orders.Tests/Unit/         # ORD001Tests through ORD009Tests
├── Restaurant.Tests/Unit/     # RST001Tests through RST009Tests
└── Delivery.Tests/Unit/       # DLV001Tests through DLV009Tests, GeoCalculationsTests
```

---

## 8. Error Handling Strategy

### 8.1 HTTP Functions

All HTTP functions map domain exceptions to appropriate HTTP status codes:

| Exception Type | HTTP Status | Error Code |
|---------------|-------------|------------|
| `*NotFoundException` | 404 | `*_NOT_FOUND` |
| `Invalid*Exception` | 400 | Varies by rule |
| `*CannotBe*Exception` | 409 | `*_CANNOT_*` |
| `InvalidOrderStateException` | 409 | `*_INVALID_TRANSITION` |
| Unhandled `Exception` | 500 | `INTERNAL_ERROR` |

All error responses use a consistent `ErrorResponse` DTO with `errorCode` and `message` fields.

### 8.2 Service Bus Functions

Event handler functions handle failures gracefully to prevent poison messages:

- **Order/delivery not found**: Log warning, complete message (eventual consistency -- the event may arrive before the aggregate is created, or after deletion)
- **Invalid state transition**: Log warning, complete message (idempotency -- the event may have already been processed)
- **Deserialization failure**: Log error, complete message (dead data -- retrying won't help)
- **Transient failure**: Let the function throw, Service Bus retries automatically

### 8.3 Idempotent Event Handling

All Service Bus handlers check current aggregate state before applying transitions. If the aggregate is already in the target state (or a later state), the handler logs a warning and completes the message without modification. This makes all event handlers safe to retry.

---

## 9. Testing Strategy

### 9.1 Test Layers

| Layer | What is Tested | Mocking |
|-------|---------------|---------|
| Aggregate | State transitions, business rule enforcement, domain events | None (pure domain logic) |
| Handler | Load-delegate-save orchestration, not-found handling | Repository mocked |
| Function | HTTP routing, exception-to-status mapping | MediatR mocked (where applicable) |

### 9.2 Test Coverage Per Story

Each story has a dedicated test file covering:
- Happy path (state transition succeeds, correct result returned)
- All error conditions from the AC node (not found, invalid state, rule violations)
- Domain event assertions (correct payload, correct count)
- State immutability on failure (failed operations leave aggregate unchanged)

### 9.3 Conventions

- Test class per story: `ORD001Tests`, `RST003Tests`, `DLV002Tests`
- Naming: `{Method}_{Condition}_{ExpectedResult}` (e.g., `CancelOrder_WhenStatusIsPlaced_TransitionsToCancelled`)
- Arrange/Act/Assert structure
- FluentAssertions for all assertions
- Moq for repository and mediator mocks
- No test helpers that hide assertions

---

## 10. Deployment Architecture

### 10.1 Azure Resources

```
Resource Group: rg-swarmeats-{env}
├── Azure Cosmos DB Account: cosmos-swarmeats-{env}
│   └── Database: swarmeats
│       ├── orders (partition: /customerId)
│       ├── orders-outbox (partition: /customerId)
│       ├── menus (partition: /restaurantId)
│       ├── restaurant-orders (partition: /restaurantId)
│       ├── restaurant-orders-outbox (partition: /restaurantId)
│       ├── dashboard-orders (partition: /restaurantId)
│       ├── deliveries (partition: /driverId)
│       ├── deliveries-outbox (partition: /driverId)
│       └── leases (change feed leases)
│
├── Azure Service Bus Namespace: sb-swarmeats-{env}
│   ├── Topic: orders.placed
│   │   └── Subscription: restaurant-subscription
│   ├── Topic: orders.cancelled
│   │   ├── Subscription: restaurant-subscription
│   │   └── Subscription: delivery-subscription
│   ├── Topic: restaurant.order-accepted
│   │   └── Subscription: orders-subscription
│   ├── Topic: restaurant.order-rejected
│   │   └── Subscription: orders-subscription
│   ├── Topic: restaurant.order-ready
│   │   ├── Subscription: orders-subscription
│   │   └── Subscription: delivery-subscription
│   ├── Topic: delivery.driver-assigned
│   │   └── Subscription: orders-subscription
│   └── Topic: delivery.completed
│       └── Subscription: orders-subscription
│
├── Azure Container Apps Environment: cae-swarmeats-{env}
│   ├── Container App: ca-orders-{env} (Orders Function App)
│   ├── Container App: ca-restaurant-{env} (Restaurant Function App)
│   └── Container App: ca-delivery-{env} (Delivery Function App)
│
└── Azure Application Insights: ai-swarmeats-{env}
```

### 10.2 Scaling

- HTTP-triggered functions scale based on concurrent request count
- Service Bus-triggered functions scale via KEDA based on queue depth
- Timer-triggered functions run as singletons (single instance)
- Cosmos Change Feed functions scale based on lease partitions
