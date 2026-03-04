# ORD-002 Review Fixes

## REV-001 — OrderLineItem is a mutable class violating aggregate encapsulation
- **Category**: ddd_compliance | **Severity**: must_fix
- **What was wrong**: `OrderLineItem` was declared as a mutable class with public setters on all properties (`MenuItemId`, `MenuItemName`, `Quantity`, `UnitPrice`). Any external code could modify line item data after creation, bypassing the Order aggregate's invariant protection. As a value object with no independent identity lifecycle, it should not have been mutable.
- **What was fixed**: Converted `OrderLineItem` from a `class` to a `record` with `init`-only properties. Added a public constructor that validates `quantity >= 1` and `unitPrice > 0` (per FDD business rules). Added a private parameterless constructor for Cosmos DB deserialization compatibility. Updated the test helper in `ORD002Tests.cs` to use the new constructor syntax instead of object initializers with setters.
- **Files changed**: `src/Orders/Domain/Aggregates/Order.cs`, `tests/Orders.Tests/Unit/ORD002Tests.cs`
- **AC reference**: ORD-R05 (aggregate encapsulation), FDD business rules for line item validation

## REV-002 — OutboxMessage and ErrorResponse declared inside unrelated files
- **Category**: ddd_compliance | **Severity**: should_fix
- **What was wrong**: `OutboxMessage` was declared as an internal sealed class inside `CosmosOrderRepository.cs`, coupling the outbox data model to a single repository file and making it unreachable by the Cosmos Change Feed processor without violating single-responsibility. `ErrorResponse` was declared inside `CancelOrderFunction.cs` and would be duplicated when `PlaceOrder` (ORD-001) and `GetOrder` (ORD-003) functions are implemented.
- **What was fixed**: Extracted `OutboxMessage` to its own file at `src/Orders/Infrastructure/Models/OutboxMessage.cs` with namespace `Orders.Infrastructure.Models`. Extracted `ErrorResponse` to its own file at `src/Orders/Functions/Models/ErrorResponse.cs` with namespace `Orders.Functions.Models`. Added `using` statements to the original files referencing the new namespaces. Both classes retain their `internal sealed` access modifier.
- **Files changed**: `src/Orders/Infrastructure/Repositories/CosmosOrderRepository.cs`, `src/Orders/Infrastructure/Models/OutboxMessage.cs` (new), `src/Orders/Functions/CancelOrderFunction.cs`, `src/Orders/Functions/Models/ErrorResponse.cs` (new)
- **AC reference**: N/A (structural improvement for maintainability)

## REV-003 — Rejection tests do not verify aggregate state remains unmodified
- **Category**: test_honesty | **Severity**: should_fix
- **What was wrong**: The two rejection tests (`CancelOrder_WhenStatusIsAccepted_ThrowsOrderCannotBeCancelledException` and `CancelOrder_WhenStatusIsDelivered_ThrowsOrderCannotBeCancelledException`) only asserted that the correct exception was thrown. They did not verify that the aggregate state remained completely unmodified after the failed cancellation. A future refactoring could introduce a bug where the aggregate partially mutates before throwing (e.g., sets `CancelledAt` then throws), and the tests would still pass.
- **What was fixed**: Added three additional assertions after the exception check in both tests: `order.Status.Should().Be(OrderStatus.Accepted/Delivered)` to confirm status is unchanged, `order.CancelledAt.Should().BeNull()` to confirm no cancellation timestamp was set, and `order.DomainEvents.Should().BeEmpty()` to confirm no domain events were raised. The test structure already captured the `order` reference before the Act step, so no restructuring was needed.
- **Files changed**: `tests/Orders.Tests/Unit/ORD002Tests.cs`
- **AC reference**: ORD-002-ERR-02 (rejection must have zero side effects)

## Summary
- Findings fixed: 3/3
- Must-fix resolved: 1/1
- Should-fix resolved: 2/2
- New files created: `src/Orders/Infrastructure/Models/OutboxMessage.cs`, `src/Orders/Functions/Models/ErrorResponse.cs`
