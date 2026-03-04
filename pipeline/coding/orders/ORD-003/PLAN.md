# ORD-003 Implementation Plan — Get Order by ID

## Story
As a customer, I want to view the complete details of my order including line items, current status, and timestamps so that I can track its progress through the fulfilment lifecycle.

## Approach
Simple CQRS query — load the Order aggregate from Cosmos DB and map it to a flat response DTO. No state changes, no domain events, no business rule enforcement.

## DDD Pattern
`query_handler` — MediatR IRequestHandler that loads from the repository and maps to a response DTO. The handler contains zero business logic.

## Files to Create
1. `src/Orders/Domain/Queries/GetOrderQuery.cs` — MediatR IRequest + response DTOs (GetOrderResponse, GetOrderLineItemResponse, GetOrderDeliveryAddressResponse, GetOrderTotalResponse, GetOrderTimestampsResponse)
2. `src/Orders/Domain/Queries/GetOrderQueryHandler.cs` — MediatR handler: load from IOrderRepository, throw if null, map to response
3. `src/Orders/Functions/GetOrderFunction.cs` — HTTP GET trigger at /orders/{orderId}, MediatR dispatch, exception-to-HTTP mapping
4. `tests/Orders.Tests/Unit/ORD003Tests.cs` — 5 xUnit tests covering full response mapping and 404 error case

## Reused from ORD-002
- `IOrderRepository.GetByIdAsync` — existing repository method
- `OrderNotFoundException` — existing domain exception
- `ErrorResponse` — extracted to Functions/Models/ErrorResponse.cs in Layer 6 fix
- `Order` aggregate + value objects — existing domain model

## Acceptance Criteria Coverage
| AC ID | Description | Test |
|-------|-------------|------|
| ORD-003-AC-01 | Full order returned with line items, status, timestamps | 4 tests (details, line items, address/totals, timestamps) |
| ORD-003-AC-02 | HTTP 200 returned with complete order object | Covered by handler returning GetOrderResponse |
| ORD-003-ERR-01 | HTTP 404 when order not found | GetOrder_WhenOrderNotFound_ThrowsOrderNotFoundException |

## Round
Round 1 — no cross-context dependencies. Reuses existing Order aggregate from ORD-002.
