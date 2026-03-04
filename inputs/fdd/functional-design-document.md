# SwarmEats — Online Food Ordering Platform

## Functional Design Document

**Version:** 1.0
**Date:** 2025-02-14
**Author:** Product & Architecture Team
**Status:** Approved for development

---

## 1. Executive Summary

SwarmEats is an online food ordering platform that connects customers with local restaurants for delivery. Customers browse restaurant menus, place orders, and track delivery in real time. Restaurants receive orders, manage preparation, and signal when food is ready for pickup. Drivers are assigned to deliveries, provide live location updates, and confirm delivery completion.

The platform is built as a set of independent bounded contexts communicating through domain events over Azure Service Bus. Each context owns its data in Azure Cosmos DB and exposes its capabilities through Azure Functions using the isolated worker model.

This document specifies the business rules, data models, API contracts, domain events, and error handling for the initial release covering three bounded contexts: Orders, Restaurant, and Delivery.

---

## 2. System Overview

### 2.1 Technology Stack

- **Runtime:** .NET 8, Azure Functions isolated worker model
- **Persistence:** Azure Cosmos DB (serverless, NoSQL document model)
- **Messaging:** Azure Service Bus (Standard tier, topic/subscription model)
- **Hosting:** Azure Container Apps (serverless containers)
- **Authentication:** Azure AD B2C for customer-facing APIs; API key for inter-service calls
- **Patterns:** Domain-Driven Design, CQRS with MediatR, outbox pattern for reliable event publishing

### 2.2 Bounded Contexts

| Context | Purpose | Primary Users |
|---------|---------|---------------|
| Orders | Order placement, validation, pricing, and lifecycle management | Customers |
| Restaurant | Menu management, order acceptance/rejection, preparation tracking | Restaurant staff |
| Delivery | Driver assignment, location tracking, delivery completion | Drivers |

### 2.3 High-Level Event Flow

1. A customer places an order through the Orders API.
2. The Orders context publishes an `OrderPlaced` event.
3. The Restaurant context receives the event, validates the menu items, and presents the order to restaurant staff.
4. Restaurant staff accept or reject the order. The Restaurant context publishes `OrderAccepted` or `OrderRejected`.
5. If accepted, the restaurant tracks preparation and publishes `OrderReadyForPickup` when food is ready.
6. The Delivery context receives the ready event and assigns an available driver. It publishes `DriverAssigned`.
7. The driver picks up the food, provides location updates, and completes the delivery. The Delivery context publishes `DeliveryCompleted`.
8. The Orders context consumes events from Restaurant and Delivery to update order status throughout the lifecycle.

---

## 3. Bounded Context: Orders

### 3.1 Purpose

The Orders context is the customer-facing entry point. It owns the order lifecycle from placement through final delivery confirmation. It validates order data, enforces pricing rules, and maintains order status by consuming events from the Restaurant and Delivery contexts.

### 3.2 Aggregate: Order

**Aggregate Root:** `Order`

**Entities:**
- `OrderLineItem` — one item in the order, referencing a menu item ID, quantity, and unit price

**Value Objects:**
- `DeliveryAddress` — street, city, postcode, latitude, longitude
- `OrderTotal` — subtotal, delivery fee (fixed £2.99), total

**State Machine:**

```
Draft → Placed → Accepted → Preparing → ReadyForPickup → InDelivery → Delivered
                → Rejected
       → Cancelled
```

Valid transitions:
- `Draft → Placed`: customer submits the order
- `Placed → Accepted`: restaurant accepts
- `Placed → Rejected`: restaurant rejects
- `Placed → Cancelled`: customer cancels (only while status is Placed)
- `Accepted → Preparing`: restaurant begins preparation
- `Preparing → ReadyForPickup`: restaurant signals food is ready
- `ReadyForPickup → InDelivery`: driver picks up the food
- `InDelivery → Delivered`: driver completes delivery

No other transitions are permitted. Attempting an invalid transition must raise a domain exception with error code `ORDER_INVALID_TRANSITION`.

### 3.3 Business Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| ORD-R01 | Minimum order subtotal is £10.00 (excluding delivery fee) | Validated on PlaceOrder command |
| ORD-R02 | Maximum 20 line items per order | Validated on PlaceOrder command |
| ORD-R03 | Every line item must have quantity ≥ 1 and unit price > £0.00 | Validated on PlaceOrder command |
| ORD-R04 | Delivery address must include latitude and longitude | Validated on PlaceOrder command |
| ORD-R05 | An order can only be cancelled while its status is `Placed` (before restaurant accepts) | Validated on CancelOrder command |
| ORD-R06 | Order total = sum of (line item quantity × unit price) + delivery fee (£2.99) | Calculated on PlaceOrder, stored in OrderTotal value object |
| ORD-R07 | Each order is assigned a unique order number in format `ORD-{YYYYMMDD}-{sequential}` | Generated on PlaceOrder |

### 3.4 API Endpoints

#### POST /orders — Place Order
Creates a new order. Validates all business rules, persists the order, and raises the `OrderPlaced` domain event.

**Request body:**
```json
{
  "customerId": "guid",
  "restaurantId": "guid",
  "deliveryAddress": {
    "street": "string",
    "city": "string",
    "postcode": "string",
    "latitude": "decimal",
    "longitude": "decimal"
  },
  "lineItems": [
    {
      "menuItemId": "guid",
      "menuItemName": "string",
      "quantity": "int",
      "unitPrice": "decimal"
    }
  ]
}
```

**Success response:** HTTP 201 with order ID, order number, status, and calculated total.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Subtotal below £10.00 | 400 | `ORDER_MINIMUM_NOT_MET` |
| More than 20 line items | 400 | `ORDER_TOO_MANY_ITEMS` |
| Line item quantity < 1 or price ≤ 0 | 400 | `ORDER_INVALID_LINE_ITEM` |
| Missing latitude/longitude | 400 | `ORDER_INVALID_ADDRESS` |
| Customer ID is empty | 400 | `ORDER_INVALID_CUSTOMER` |
| Restaurant ID is empty | 400 | `ORDER_INVALID_RESTAURANT` |

#### POST /orders/{orderId}/cancel — Cancel Order
Cancels an order. Only permitted while status is `Placed`.

**Success response:** HTTP 200 with updated order (status = Cancelled).

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Order not found | 404 | `ORDER_NOT_FOUND` |
| Order status is not `Placed` | 409 | `ORDER_CANNOT_CANCEL` |

#### GET /orders/{orderId} — Get Order
Returns the full order including line items, status, and timestamps for each state transition.

**Success response:** HTTP 200 with full order object.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Order not found | 404 | `ORDER_NOT_FOUND` |

#### GET /orders?customerId={customerId} — Get Orders by Customer
Returns all orders for a customer, sorted by creation date descending. Supports pagination via `continuationToken` query parameter.

**Success response:** HTTP 200 with array of order summaries (id, order number, restaurant name, total, status, created date) and optional continuation token.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Customer ID is empty | 400 | `ORDER_INVALID_CUSTOMER` |

### 3.5 Domain Events Raised

#### OrderPlaced
Published to Service Bus topic `orders.placed` when a customer places an order.

**Payload:**
| Field | Type | Description |
|-------|------|-------------|
| orderId | Guid | Unique order identifier |
| orderNumber | string | Human-readable order number |
| customerId | Guid | Customer identifier |
| restaurantId | Guid | Restaurant identifier |
| lineItems | array | Array of { menuItemId: Guid, menuItemName: string, quantity: int, unitPrice: decimal } |
| deliveryAddress | object | { street, city, postcode, latitude, longitude } |
| orderTotal | decimal | Calculated total including delivery fee |
| placedAt | DateTimeOffset | Timestamp of order placement |

#### OrderCancelled
Published to Service Bus topic `orders.cancelled` when a customer cancels an order.

**Payload:**
| Field | Type | Description |
|-------|------|-------------|
| orderId | Guid | Unique order identifier |
| orderNumber | string | Human-readable order number |
| restaurantId | Guid | Restaurant identifier |
| cancelledAt | DateTimeOffset | Timestamp of cancellation |

### 3.6 Domain Events Consumed

| Event | Source Topic | Action |
|-------|-------------|--------|
| `OrderAccepted` | `restaurant.order-accepted` | Transition order status to `Accepted` |
| `OrderRejected` | `restaurant.order-rejected` | Transition order status to `Rejected` |
| `OrderReadyForPickup` | `restaurant.order-ready` | Transition order status to `ReadyForPickup` |
| `DriverAssigned` | `delivery.driver-assigned` | Transition order status to `InDelivery` |
| `DeliveryCompleted` | `delivery.completed` | Transition order status to `Delivered` |

Each consumed event triggers a status transition on the Order aggregate. If the transition is invalid (e.g., receiving `OrderAccepted` for an already cancelled order), the event handler must log a warning and discard the event without throwing an exception. This prevents poison message scenarios on the Service Bus.

### 3.7 Cosmos DB

- **Container:** `orders`
- **Partition key:** `/customerId`
- **Document:** One document per order containing the full aggregate state

---

## 4. Bounded Context: Restaurant

### 4.1 Purpose

The Restaurant context manages restaurant menus and handles incoming orders from the Orders context. Restaurant staff use this context to review incoming orders, accept or reject them, and track preparation through to pickup readiness.

### 4.2 Aggregate: Menu

**Aggregate Root:** `Menu`

**Entities:**
- `MenuItem` — a single item on the menu with name, description, price, category, and availability flag

**Value Objects:**
- `Price` — amount (decimal), currency (string, always "GBP" for initial release)
- `PreparationTime` — estimated minutes to prepare this item

A restaurant has exactly one active menu at any time. The menu is identified by the restaurant ID.

### 4.3 Aggregate: RestaurantOrder

**Aggregate Root:** `RestaurantOrder`

This is the Restaurant context's local representation of an incoming order. It is created when an `OrderPlaced` event is received and tracks the restaurant's processing of that order.

**State Machine:**

```
Pending → Accepted → Preparing → ReadyForPickup
        → Rejected
```

Valid transitions:
- `Pending → Accepted`: staff accepts the order (must provide estimated preparation time)
- `Pending → Rejected`: staff rejects the order (must provide reason code)
- `Accepted → Preparing`: staff begins preparation
- `Preparing → ReadyForPickup`: staff signals food is ready for driver pickup

### 4.4 Business Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| RST-R01 | A restaurant must be within operating hours to accept orders. Operating hours are stored on the Menu aggregate as `openingTime` and `closingTime` (UTC). | Validated when processing incoming `OrderPlaced` event. If outside hours, auto-reject with reason code `RESTAURANT_CLOSED`. |
| RST-R02 | Every line item in an incoming order must reference a menu item that exists and is marked as available. | Validated when processing incoming `OrderPlaced` event. If any item is unavailable, auto-reject with reason code `ITEM_UNAVAILABLE` and include the unavailable item IDs in the rejection payload. |
| RST-R03 | When accepting an order, staff must provide an estimated preparation time between 5 and 90 minutes. | Validated on AcceptOrder command. |
| RST-R04 | When rejecting an order, staff must provide a reason code from the allowed set: `RESTAURANT_CLOSED`, `ITEM_UNAVAILABLE`, `TOO_BUSY`, `OTHER`. | Validated on RejectOrder command. |
| RST-R05 | An order can only be rejected while its status is `Pending`. Once accepted, it must be completed. | Validated on RejectOrder command. |
| RST-R06 | Menu item prices must be greater than £0.00 and preparation time must be at least 1 minute. | Validated on menu item creation/update. |

### 4.5 API Endpoints

#### GET /restaurants/{restaurantId}/menu — Get Menu
Returns the full menu for a restaurant including all items, availability, and operating hours.

**Success response:** HTTP 200 with menu object.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Restaurant not found | 404 | `RESTAURANT_NOT_FOUND` |

#### GET /restaurants/{restaurantId}/orders?status={status} — Get Active Orders
Returns orders for a restaurant filtered by status. Used by restaurant dashboard.

**Success response:** HTTP 200 with array of restaurant order summaries.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Invalid status value | 400 | `RESTAURANT_INVALID_STATUS` |

#### POST /restaurants/{restaurantId}/orders/{orderId}/accept — Accept Order
Staff accepts an incoming order with estimated preparation time.

**Request body:**
```json
{
  "estimatedPrepMinutes": "int"
}
```

**Success response:** HTTP 200 with updated restaurant order.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Order not found | 404 | `RESTAURANT_ORDER_NOT_FOUND` |
| Order not in Pending status | 409 | `RESTAURANT_ORDER_NOT_PENDING` |
| Preparation time outside 5-90 range | 400 | `RESTAURANT_INVALID_PREP_TIME` |

#### POST /restaurants/{restaurantId}/orders/{orderId}/reject — Reject Order
Staff rejects an incoming order with a reason code.

**Request body:**
```json
{
  "reasonCode": "string",
  "notes": "string (optional)"
}
```

**Success response:** HTTP 200 with updated restaurant order.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Order not found | 404 | `RESTAURANT_ORDER_NOT_FOUND` |
| Order not in Pending status | 409 | `RESTAURANT_ORDER_NOT_PENDING` |
| Invalid reason code | 400 | `RESTAURANT_INVALID_REASON` |

#### PUT /restaurants/{restaurantId}/orders/{orderId}/status — Update Preparation Status
Staff updates the order to `Preparing` or `ReadyForPickup`.

**Request body:**
```json
{
  "status": "string"
}
```

**Success response:** HTTP 200 with updated restaurant order.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Order not found | 404 | `RESTAURANT_ORDER_NOT_FOUND` |
| Invalid status transition | 409 | `RESTAURANT_INVALID_TRANSITION` |

### 4.6 Domain Events Raised

#### OrderAccepted
Published to Service Bus topic `restaurant.order-accepted`.

**Payload:**
| Field | Type | Description |
|-------|------|-------------|
| orderId | Guid | Original order ID from Orders context |
| restaurantId | Guid | Restaurant identifier |
| estimatedPrepMinutes | int | Estimated preparation time |
| acceptedAt | DateTimeOffset | Timestamp |

#### OrderRejected
Published to Service Bus topic `restaurant.order-rejected`.

**Payload:**
| Field | Type | Description |
|-------|------|-------------|
| orderId | Guid | Original order ID from Orders context |
| restaurantId | Guid | Restaurant identifier |
| reasonCode | string | Rejection reason code |
| unavailableItemIds | array of Guid | Menu items that were unavailable (empty if not applicable) |
| rejectedAt | DateTimeOffset | Timestamp |

#### OrderReadyForPickup
Published to Service Bus topic `restaurant.order-ready`.

**Payload:**
| Field | Type | Description |
|-------|------|-------------|
| orderId | Guid | Original order ID from Orders context |
| restaurantId | Guid | Restaurant identifier |
| restaurantAddress | object | { street, city, postcode, latitude, longitude } |
| readyAt | DateTimeOffset | Timestamp |

### 4.7 Domain Events Consumed

| Event | Source Topic | Action |
|-------|-------------|--------|
| `OrderPlaced` | `orders.placed` | Create a new `RestaurantOrder` in `Pending` status. Validate menu items and operating hours. If validation fails, auto-reject immediately. |
| `OrderCancelled` | `orders.cancelled` | If restaurant order is still `Pending`, mark as cancelled and do not present to staff. If already `Accepted` or later, log a warning — the food is already being prepared. |

### 4.8 Change Feed: Active Order Dashboard

A Cosmos DB Change Feed trigger monitors the `restaurant-orders` container. When a restaurant order document changes, the function pushes the updated order state to a read-optimised view used by the restaurant dashboard. This function does not raise domain events — it is a projection for query performance.

### 4.9 Cosmos DB

- **Container:** `menus` — one document per restaurant menu, partition key `/restaurantId`
- **Container:** `restaurant-orders` — one document per incoming order, partition key `/restaurantId`

---

## 5. Bounded Context: Delivery

### 5.1 Purpose

The Delivery context manages the assignment of drivers to orders that are ready for pickup, tracks driver location during delivery, and confirms delivery completion. It is triggered by the Restaurant context's `OrderReadyForPickup` event.

### 5.2 Aggregate: Delivery

**Aggregate Root:** `Delivery`

**Value Objects:**
- `DriverLocation` — latitude, longitude, recorded timestamp
- `Route` — restaurant address (pickup), delivery address (dropoff)
- `EstimatedArrival` — estimated time of arrival at delivery address, updated with each location ping

**State Machine:**

```
AwaitingDriver → DriverAssigned → PickedUp → Delivered
```

Valid transitions:
- `AwaitingDriver → DriverAssigned`: a driver is assigned to this delivery
- `DriverAssigned → PickedUp`: driver confirms pickup at restaurant
- `PickedUp → Delivered`: driver confirms delivery at customer address

### 5.3 Business Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| DLV-R01 | A driver can only be assigned to one active delivery at a time. A driver is considered "available" when they have no delivery in `DriverAssigned` or `PickedUp` status. | Validated on AssignDriver command. |
| DLV-R02 | The assigned driver must be within 5km of the restaurant at the time of assignment. Distance is calculated as straight-line distance using the Haversine formula between the driver's last known location and the restaurant's latitude/longitude. | Validated on AssignDriver command. |
| DLV-R03 | The total delivery time SLA is 45 minutes from `OrderReadyForPickup` to `Delivered`. The estimated arrival time is calculated as: ready time + estimated drive time. This is informational — the SLA is not enforced as a hard constraint, but breaches are logged. | Calculated on driver assignment and updated on location pings. |
| DLV-R04 | Driver location updates must include latitude, longitude, and a timestamp. Updates are accepted only for deliveries in `DriverAssigned` or `PickedUp` status. | Validated on UpdateDriverLocation command. |
| DLV-R05 | A delivery can only be completed when its status is `PickedUp`. Attempting to complete a delivery that hasn't been picked up must fail. | Validated on CompleteDelivery command. |

### 5.4 API Endpoints

#### GET /deliveries/{deliveryId} — Get Delivery Status
Returns the delivery details including current status, driver location, route, and estimated arrival.

**Success response:** HTTP 200 with full delivery object.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Delivery not found | 404 | `DELIVERY_NOT_FOUND` |

#### GET /deliveries/{deliveryId}/eta — Get Estimated Arrival
Returns the current estimated arrival time based on the driver's last known location.

**Success response:** HTTP 200 with estimated arrival time and last location update timestamp.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Delivery not found | 404 | `DELIVERY_NOT_FOUND` |
| No driver assigned yet | 409 | `DELIVERY_NO_DRIVER` |

#### POST /deliveries/{deliveryId}/assign — Assign Driver
Assigns an available driver to a delivery awaiting a driver.

**Request body:**
```json
{
  "driverId": "guid",
  "driverLocation": {
    "latitude": "decimal",
    "longitude": "decimal"
  }
}
```

**Success response:** HTTP 200 with updated delivery (status = DriverAssigned).

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Delivery not found | 404 | `DELIVERY_NOT_FOUND` |
| Delivery not in AwaitingDriver status | 409 | `DELIVERY_ALREADY_ASSIGNED` |
| Driver already has an active delivery | 409 | `DRIVER_NOT_AVAILABLE` |
| Driver too far from restaurant (> 5km) | 400 | `DRIVER_TOO_FAR` |

#### PUT /deliveries/{deliveryId}/location — Update Driver Location
Driver sends a location ping during delivery.

**Request body:**
```json
{
  "driverId": "guid",
  "latitude": "decimal",
  "longitude": "decimal"
}
```

**Success response:** HTTP 200 with updated estimated arrival time.

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Delivery not found | 404 | `DELIVERY_NOT_FOUND` |
| Delivery not in DriverAssigned or PickedUp status | 409 | `DELIVERY_NOT_ACTIVE` |
| Driver ID doesn't match assigned driver | 403 | `DELIVERY_WRONG_DRIVER` |

#### POST /deliveries/{deliveryId}/pickup — Confirm Pickup
Driver confirms they have picked up the food from the restaurant.

**Request body:**
```json
{
  "driverId": "guid"
}
```

**Success response:** HTTP 200 with updated delivery (status = PickedUp).

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Delivery not found | 404 | `DELIVERY_NOT_FOUND` |
| Delivery not in DriverAssigned status | 409 | `DELIVERY_INVALID_TRANSITION` |
| Driver ID doesn't match assigned driver | 403 | `DELIVERY_WRONG_DRIVER` |

#### POST /deliveries/{deliveryId}/complete — Complete Delivery
Driver confirms delivery to the customer.

**Request body:**
```json
{
  "driverId": "guid"
}
```

**Success response:** HTTP 200 with updated delivery (status = Delivered).

**Error responses:**
| Condition | HTTP Status | Error Code |
|-----------|------------|------------|
| Delivery not found | 404 | `DELIVERY_NOT_FOUND` |
| Delivery not in PickedUp status | 409 | `DELIVERY_NOT_PICKED_UP` |
| Driver ID doesn't match assigned driver | 403 | `DELIVERY_WRONG_DRIVER` |

### 5.5 Domain Events Raised

#### DriverAssigned
Published to Service Bus topic `delivery.driver-assigned`.

**Payload:**
| Field | Type | Description |
|-------|------|-------------|
| deliveryId | Guid | Delivery identifier |
| orderId | Guid | Original order ID from Orders context |
| driverId | Guid | Assigned driver identifier |
| estimatedArrivalMinutes | int | Estimated minutes until delivery |
| assignedAt | DateTimeOffset | Timestamp |

#### DeliveryCompleted
Published to Service Bus topic `delivery.completed`.

**Payload:**
| Field | Type | Description |
|-------|------|-------------|
| deliveryId | Guid | Delivery identifier |
| orderId | Guid | Original order ID from Orders context |
| driverId | Guid | Driver who completed the delivery |
| deliveredAt | DateTimeOffset | Timestamp of delivery completion |
| totalDeliveryMinutes | int | Minutes from OrderReadyForPickup to Delivered |
| slaBreached | bool | True if totalDeliveryMinutes > 45 |

### 5.6 Domain Events Consumed

| Event | Source Topic | Action |
|-------|-------------|--------|
| `OrderReadyForPickup` | `restaurant.order-ready` | Create a new `Delivery` in `AwaitingDriver` status with the restaurant address as pickup location and the order's delivery address as dropoff location. |
| `OrderCancelled` | `orders.cancelled` | If delivery is in `AwaitingDriver` status, cancel the delivery. If a driver is already assigned or has picked up, log a warning — the driver should be notified out of band. |

### 5.7 Timer Trigger: Unassigned Delivery Check

A timer-triggered Azure Function runs every 30 seconds. It queries the `deliveries` container for all deliveries in `AwaitingDriver` status that have been waiting longer than 5 minutes. For each, it logs a warning and increments a metric counter. This function does not auto-assign drivers — assignment is manual through the API. The timer function exists for operational monitoring.

### 5.8 Cosmos DB

- **Container:** `deliveries` — one document per delivery, partition key `/driverId`
- Note: deliveries in `AwaitingDriver` status (no driver yet) use a sentinel partition key value of `unassigned`. Once a driver is assigned, the document is deleted and recreated under the driver's partition key. This avoids hot partition issues on the sentinel value.

---

## 6. Integration Patterns

### 6.1 Messaging

All inter-context communication uses Azure Service Bus topics with subscriptions. Each consuming context creates its own subscription on the publishing context's topic.

| Topic | Publisher | Subscribers |
|-------|-----------|-------------|
| `orders.placed` | Orders | Restaurant |
| `orders.cancelled` | Orders | Restaurant, Delivery |
| `restaurant.order-accepted` | Restaurant | Orders |
| `restaurant.order-rejected` | Restaurant | Orders |
| `restaurant.order-ready` | Restaurant | Delivery |
| `delivery.driver-assigned` | Delivery | Orders |
| `delivery.completed` | Delivery | Orders |

### 6.2 Event Contracts

All domain events are published as JSON messages on Service Bus with the following envelope:

```json
{
  "eventType": "string",
  "eventId": "guid",
  "occurredAt": "datetimeoffset",
  "sourceContext": "string",
  "payload": { }
}
```

The `eventType` matches the event class name (e.g., `OrderPlaced`). The `payload` contains the event-specific fields defined in each context's domain events section.

### 6.3 Reliable Publishing

All contexts use the outbox pattern for event publishing. Domain events are persisted atomically with the aggregate state change in the same Cosmos DB transaction (using the transactional batch API within the same partition). A background process reads unpublished events from the outbox and publishes them to Service Bus, marking them as published after successful send.

This ensures that:
- An event is never published without the corresponding state change being persisted
- A state change never occurs without the corresponding event being published (eventual consistency)

### 6.4 Idempotent Event Handling

All event handlers must be idempotent. Service Bus guarantees at-least-once delivery. Event handlers must check whether they have already processed an event (using the `eventId`) before applying state changes. Duplicate events must be silently discarded.

---

## 7. Data Model

### 7.1 Cosmos DB Configuration

| Container | Partition Key | Context | TTL | Notes |
|-----------|--------------|---------|-----|-------|
| `orders` | `/customerId` | Orders | None | Long-lived order records |
| `orders-outbox` | `/orderId` | Orders | 7 days | Outbox events, cleaned up after publishing |
| `menus` | `/restaurantId` | Restaurant | None | One menu document per restaurant |
| `restaurant-orders` | `/restaurantId` | Restaurant | 30 days | Incoming order records |
| `restaurant-outbox` | `/restaurantId` | Restaurant | 7 days | Outbox events |
| `deliveries` | `/driverId` | Delivery | 30 days | Delivery records |
| `deliveries-outbox` | `/deliveryId` | Delivery | 7 days | Outbox events |

### 7.2 Indexing Policy

Use the default Cosmos DB indexing policy (index all paths) for the initial release. Optimise with custom indexing policies after load testing identifies bottlenecks.

---

## 8. Non-Functional Requirements

### 8.1 Performance

- Order placement API: < 500ms p95 latency
- Menu retrieval API: < 200ms p95 latency
- Driver location update API: < 300ms p95 latency
- Event processing (end-to-end from publish to handler completion): < 5 seconds p95

### 8.2 Availability

- Target 99.9% availability for all customer-facing APIs
- Service Bus topic processing must handle transient failures with retry (exponential backoff, max 5 retries)
- Dead-letter queue monitoring for all subscriptions

### 8.3 Scaling

- Orders context: scale to handle 100 concurrent order placements
- Restaurant context: scale to handle 500 restaurants with 50 concurrent orders each
- Delivery context: scale to handle 200 active deliveries with location pings every 10 seconds

### 8.4 Observability

- All Azure Functions log structured events using ILogger
- Every API request logs: correlation ID, bounded context, function name, duration, status code
- Every event handler logs: correlation ID, event type, source context, processing duration, outcome
- Cosmos DB request unit (RU) consumption logged per operation for cost monitoring
