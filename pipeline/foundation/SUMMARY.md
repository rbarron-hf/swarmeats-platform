# Context Foundation Summary — SwarmEats

## Bounded Contexts Identified

**3 bounded contexts** extracted from the FDD:

1. **Orders** — Customer-facing order placement, validation, pricing, and lifecycle management. Owns the Order aggregate (with OrderLineItem entities, DeliveryAddress and OrderTotal value objects). Tracks order state from Draft through Delivered via a 9-state machine. Consumes events from both Restaurant and Delivery to update status.

2. **Restaurant** — Menu management and incoming order processing. Owns two aggregates: Menu (with MenuItem entities, Price and PreparationTime value objects) and RestaurantOrder (the restaurant's local representation of an incoming order). Auto-validates incoming orders against menu availability and operating hours. Tracks preparation through to pickup readiness.

3. **Delivery** — Driver assignment, location tracking, and delivery completion. Owns the Delivery aggregate (with DriverLocation, Route, and EstimatedArrival value objects). Enforces driver proximity (5km Haversine) and availability constraints. Monitors unassigned deliveries via a 30-second timer.

## Key Integration Patterns

All inter-context communication is **event-driven via Azure Service Bus** (topic/subscription model). There are **7 Service Bus topics** carrying domain events:

- Orders → Restaurant: `orders.placed`, `orders.cancelled`
- Restaurant → Orders: `restaurant.order-accepted`, `restaurant.order-rejected`, `restaurant.order-ready`
- Restaurant → Delivery: `restaurant.order-ready`
- Orders → Delivery: `orders.cancelled`
- Delivery → Orders: `delivery.driver-assigned`, `delivery.completed`

All contexts use the **outbox pattern** for reliable event publishing (atomic persistence + event recording in Cosmos DB transactional batch).

## Infrastructure Summary

- **26 Azure Functions** across 3 contexts (15 HTTP, 9 Service Bus, 1 Cosmos Change Feed, 1 Timer)
- **7 Cosmos DB containers** (orders, orders-outbox, menus, restaurant-orders, restaurant-outbox, deliveries, deliveries-outbox)
- **7 Service Bus topics** with **9 subscriptions**
- **4 aggregates**, **10 commands**, **6 queries**, **7 domain events**

## Ubiquitous Language

**77 domain terms** catalogued across all 3 contexts. Ambiguity flags set on terms that appear in multiple contexts with different meanings (e.g., order statuses like "Accepted" and "Preparing" exist in both Orders and Restaurant contexts with context-specific semantics).

## Flags

No unresolved ambiguities. The FDD is fully specified for all 3 bounded contexts.
