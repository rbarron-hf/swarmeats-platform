# SwarmEats .NET Template Structure

Reference template for generating Azure Functions isolated worker projects within the SwarmEats platform. This template defines the canonical directory layout, file naming conventions, and code patterns that every bounded context must follow.

## Purpose

This template serves as the **structural blueprint** for Layer 5 (Coding Agent) of the AI Swarm Pipeline. When the pipeline generates code for a new bounded context, it uses this template to ensure:

- Consistent project structure across all contexts
- Uniform naming conventions and namespace patterns
- Standardised DDD patterns (aggregates, value objects, events, commands, queries)
- Correct Azure Functions trigger patterns (HTTP, Service Bus, Timer, Change Feed)
- Proper infrastructure patterns (Cosmos DB repository, outbox, concurrency)

## Directory Layout

```
dotnet-template/
├── SwarmEats.sln                          # Solution with src + test projects
├── Directory.Build.props                  # Shared build properties (net8.0, nullable, etc.)
├── Directory.Packages.props               # Central package management (pinned versions)
├── .editorconfig                          # Code style enforcement
├── global.json                            # SDK version pinning
├── .gitignore                             # Standard .NET + Azure Functions ignores
│
├── src/Context.Template/                  # Source project (rename per context)
│   ├── Context.Template.csproj            # Function app project (no version attrs)
│   ├── host.json                          # Functions host configuration
│   ├── local.settings.template.json       # Template for local dev settings
│   ├── Program.cs                         # Host bootstrap with DI registration
│   │
│   ├── Domain/
│   │   ├── Aggregates/
│   │   │   └── AggregateRoot.cs           # Generic base with Id, ETag, domain events
│   │   ├── Commands/
│   │   │   ├── _COMMAND_TEMPLATE.cs       # MediatR IRequest command record
│   │   │   └── _COMMAND_HANDLER_TEMPLATE.cs  # Load-delegate-save handler
│   │   ├── Queries/
│   │   │   ├── _QUERY_TEMPLATE.cs         # MediatR IRequest query record
│   │   │   └── _QUERY_HANDLER_TEMPLATE.cs # Read-only handler with mapping
│   │   ├── Events/
│   │   │   ├── IDomainEvent.cs            # Marker interface
│   │   │   ├── _EVENT_TEMPLATE.cs         # Domain event raised by this context
│   │   │   └── Consumed/
│   │   │       └── _CONSUMED_EVENT_TEMPLATE.cs  # Anti-corruption layer envelope
│   │   ├── Exceptions/
│   │   │   └── _EXCEPTION_TEMPLATE.cs     # Domain exception with error codes
│   │   └── ValueObjects/
│   │       └── _VALUE_OBJECT_TEMPLATE.cs  # Immutable record with validation
│   │
│   ├── Infrastructure/
│   │   ├── Models/
│   │   │   └── OutboxMessage.cs           # Outbox message for reliable eventing
│   │   └── Repositories/
│   │       ├── _REPOSITORY_INTERFACE_TEMPLATE.cs   # Domain interface
│   │       └── _COSMOS_REPOSITORY_TEMPLATE.cs      # Cosmos implementation with batch + ETag
│   │
│   └── Functions/
│       ├── Models/
│       │   └── ErrorResponse.cs           # Standard error DTO
│       ├── _HTTP_FUNCTION_TEMPLATE.cs     # HTTP trigger with MediatR dispatch
│       └── EventHandlers/
│           ├── _SERVICEBUS_FUNCTION_TEMPLATE.cs    # 5-step message handler
│           ├── _TIMER_FUNCTION_TEMPLATE.cs         # Scheduled monitoring
│           └── _CHANGEFEED_FUNCTION_TEMPLATE.cs    # Cosmos projection
│
└── tests/Context.Template.Tests/          # Test project (rename per context)
    ├── Context.Template.Tests.csproj       # xUnit + Moq + FluentAssertions
    ├── Unit/
    │   └── _TEST_TEMPLATE.cs              # Unit test patterns per story
    └── Integration/
        └── _INTEGRATION_TEST_TEMPLATE.cs  # Cosmos round-trip + concurrency tests
```

## Placeholder Conventions

Template files use the following placeholder tokens that the Coding Agent replaces:

| Placeholder | Description | Example |
|---|---|---|
| `{ContextName}` | Bounded context namespace | `Orders`, `Restaurant`, `Delivery` |
| `{Aggregate}` | Aggregate root class name | `Order`, `Menu`, `Delivery` |
| `{FunctionName}` | Azure Function class name | `PlaceOrderFunction` |
| `{CommandOrQuery}` | MediatR request type | `PlaceOrderCommand` |
| `{EventName}` | Domain event class name | `OrderPlaced` |
| `{STORY_ID}` | Story identifier | `ORD-002` |
| `{AC_NODE_ID}` | AC node identifier | `ORD-AC-002` |
| `{method}` | HTTP method (lowercase) | `post`, `get` |
| `{route}` | HTTP route pattern | `orders/{orderId}` |
| `{DocumentType}` | Change feed document type | `Order` |
| `{ProjectionType}` | Change feed projection type | `ActiveOrderView` |
| `{cron-expression}` | Timer trigger schedule | `*/30 * * * * *` |
| `{topic.name}` | Service Bus topic name | `orders.placed` |
| `{context}` | Context name (lowercase) | `restaurant` |

## Key Patterns

### Aggregate Root
All aggregates inherit from `AggregateRoot`, gaining `Id`, `ETag` (optimistic concurrency), and `DomainEvents` collection. Domain events are raised via `AddDomainEvent()` and cleared by the repository after persistence.

### Transactional Outbox
The Cosmos repository persists the aggregate and outbox messages in a single `TransactionalBatch`, ensuring atomicity. A separate process reads outbox messages and publishes to Service Bus.

### Anti-Corruption Layer
Consumed events from other contexts use a dedicated envelope type (`_CONSUMED_EVENT_TEMPLATE.cs`) with `SourceContext`, `EventType`, and a typed `Payload`. This isolates the consuming context from the producer's internal schema.

### Error Handling
HTTP functions return structured `ErrorResponse` objects. Domain exceptions carry machine-readable `ErrorCode` values (e.g., `ORDER_NOT_FOUND`) that map to HTTP status codes at the function layer.

## Renaming for a New Context

To scaffold a new bounded context from this template:

1. Copy `src/Context.Template/` to `src/{ContextName}/`
2. Copy `tests/Context.Template.Tests/` to `tests/{ContextName}.Tests/`
3. Rename `.csproj` files to match the context name
4. Find-and-replace `{ContextName}` with the actual context name
5. Update `SwarmEats.sln` to reference the new projects
6. Replace all placeholder tokens with concrete implementations
