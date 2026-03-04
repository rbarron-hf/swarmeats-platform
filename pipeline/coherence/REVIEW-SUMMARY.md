# Layer 4 Coherence Review Summary

**Project:** SwarmEats - Online Food Ordering Platform
**Date:** 2026-03-03
**Contexts Reviewed:** Orders, Restaurant, Delivery
**Stories Reviewed:** 27 | **AC Nodes Reviewed:** 27

---

## 1. Overall Health Assessment: YELLOW

The pipeline artifacts are largely consistent and well-structured across all three bounded contexts. Two critical issues require resolution before code generation can proceed safely: a missing field in the OrderReadyForPickup event payload and an uncovered business rule (RST-R06). Neither is a systemic design flaw; both are addressable with targeted additions to the existing artifacts.

---

## 2. Critical Flags

### CRIT-001: OrderReadyForPickup Event Missing deliveryAddress (Checks 2 + 4)

The OrderReadyForPickup event, as defined by the Restaurant context (RST-007), carries `orderId`, `restaurantId`, `restaurantAddress`, and `readyAt`. The Delivery context consumer (DLV-001) expects a `deliveryAddress` field to construct the Route value object (pickup + dropoff). This field does not exist in the event schema defined in system-map.yaml, the FDD, or the RST-007 AC node.

**Impact:** DLV-001 cannot set the delivery dropoff address from the event payload alone. Code generation would produce a handler that cannot populate the Route.

**Recommended Fix:** Add `deliveryAddress` to the OrderReadyForPickup event payload in the Restaurant context. The RestaurantOrder already has access to this data from the original OrderPlaced event. Update system-map.yaml (payload_fields for OrderReadyForPickup), RST-007 AC node (domain_events payload), RST-007 story, and the FDD section 4.6.

### CRIT-002: Business Rule RST-R06 Has No AC Node Coverage (Check 3)

The FDD defines RST-R06: "Menu item prices must be greater than GBP 0.00 and preparation time must be at least 1 minute, validated on menu item creation/update." The system-map.yaml lists an `UpdateMenuItem` command on the Menu aggregate, but no AC node, story, or routing decision exists for this command. This is the only FDD business rule without pipeline coverage.

**Impact:** The UpdateMenuItem capability will not be generated. Restaurant staff would have no mechanism to manage menu items.

**Recommended Fix:** Add a new AC node (RST-010) for the UpdateMenuItem command enforcing RST-R06, a corresponding story (RST-010-STORY), and a routing decision. Also add UpdateMenuItemFunction to function-topology.yaml. This would bring the total to 28 stories and 28 AC nodes.

---

## 3. Warning Flags

### WARN-001: Test Scaffolds Not Generated (Check 7)

Layer 3b was skipped. No test scaffold files exist for any of the 27 stories. Developers will need to create unit test, integration test, and contract test structures from scratch.

**Recommended Action:** Run Layer 3b test scaffold generation for all 27 stories.

### WARN-002: FDD Messaging Table Omits Orders Subscription to restaurant.order-ready (Check 2)

The FDD section 6.1 messaging table lists only Delivery as a subscriber to `restaurant.order-ready`, but the Orders context also subscribes (ORD-007 handles OrderReadyForPickup to transition order status to ReadyForPickup). The pipeline artifacts (system-map.yaml, context-map.yaml) correctly include both subscriptions. This is a documentation-only inconsistency.

**Recommended Action:** Update FDD section 6.1 to add Orders as a subscriber to `restaurant.order-ready`.

---

## 4. Dependency Graph Summary

The global dependency graph resolves cleanly into **4 tiers** with **no circular dependencies**. All 27 stories are accounted for.

| Tier | Stories | Description |
|------|---------|-------------|
| **Tier 1** | 9 stories | No cross-context dependencies. ORD-001, ORD-002, ORD-003, ORD-004 (Orders commands/queries), RST-001, RST-002 (Restaurant queries), DLV-006, DLV-007, DLV-008 (Delivery queries/monitoring) |
| **Tier 2** | 8 stories | Depend on Tier 1 cross-context events. RST-003 (consumes OrderPlaced), RST-004, RST-005, RST-006, RST-007, RST-008 (consumes OrderCancelled), RST-009, DLV-009 (consumes OrderCancelled) |
| **Tier 3** | 8 stories | Depend on Tier 2 cross-context events. DLV-001 (consumes OrderReadyForPickup), DLV-002, DLV-003, DLV-004, DLV-005, ORD-005 (consumes OrderAccepted), ORD-006 (consumes OrderRejected), ORD-007 (consumes OrderReadyForPickup) |
| **Tier 4** | 2 stories | Depend on Tier 3 cross-context events. ORD-008 (consumes DriverAssigned), ORD-009 (consumes DeliveryCompleted) |

**Key sequencing constraints:** The event chain flows Orders -> Restaurant -> Delivery -> Orders, with each tier unlocking the next wave of stories. Tier 1 stories can be developed in parallel across all three contexts immediately.

---

## 5. FDD Coverage

- **Total business rules identified:** 18 (ORD-R01 through ORD-R07, RST-R01 through RST-R06, DLV-R01 through DLV-R05)
- **Rules with AC node coverage:** 17 (94.4%)
- **Rules without coverage:** 1 (RST-R06 -- UpdateMenuItem price/prep time validation)

All Orders rules (7/7) and all Delivery rules (5/5) are fully covered. The Restaurant context covers 5 of 6 rules, missing only RST-R06 due to the absent UpdateMenuItem AC node.

---

## 6. Routing Distribution

| Routing Type | Count | Percentage | Stories |
|-------------|-------|------------|---------|
| single_agent | 24 | 88.9% | ORD-002 through ORD-009, RST-001, RST-002, RST-004 through RST-009, DLV-001, DLV-003 through DLV-009 |
| single_agent_with_subagents | 3 | 11.1% | ORD-001 (PlaceOrder), RST-003 (Process Incoming Order), DLV-002 (Assign Driver) |
| full_swarm | 0 | 0.0% | -- |

The routing distribution is appropriate. The three subagent stories are the highest-complexity nodes in their respective contexts (complexity 5, 5, and 6), each involving multiple validation rules, cross-aggregate reads, or infrastructure-level operations (outbox pattern, partition key migration, Haversine calculation). All remaining stories are correctly classified as single_agent with no over-classification detected.

**Estimated total story points:** 68 (Orders: 21, Restaurant: 24, Delivery: 23).
