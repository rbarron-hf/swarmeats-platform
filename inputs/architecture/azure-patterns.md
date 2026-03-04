# Azure Functions Patterns — SwarmEats

## 1. Runtime Model

All functions use the **Azure Functions isolated worker model** on **.NET 8**. This decouples the function runtime from the host, giving full control over dependency injection, middleware, and serialization.

```
Microsoft.Azure.Functions.Worker
Microsoft.Azure.Functions.Worker.Sdk
Microsoft.Azure.Functions.Worker.Extensions.Http
Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
Microsoft.Azure.Functions.Worker.Extensions.CosmosDB
Microsoft.Azure.Functions.Worker.Extensions.Timer
```

### Program.cs Bootstrap

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // MediatR registration
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Cosmos DB client (singleton, reused across requests)
        services.AddSingleton(sp =>
        {
            var connectionString = Environment.GetEnvironmentVariable("CosmosDBConnection");
            return new CosmosClient(connectionString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        });

        // Repository registrations (scoped)
        services.AddScoped<IOrderRepository>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClient>();
            var database = Environment.GetEnvironmentVariable("CosmosDBDatabase") ?? "swarmeats";
            return new CosmosOrderRepository(client, database);
        });
    })
    .Build();

host.Run();
```

---

## 2. HTTP Trigger Pattern

### Standard Structure

Every HTTP function follows this structure:

1. **Parse and validate route/query parameters** (return 400 if invalid)
2. **Dispatch command/query via MediatR** (zero business logic in the function)
3. **Map domain exceptions to HTTP status codes** (catch blocks)
4. **Return structured JSON response** (result DTO or ErrorResponse)

### Template

```csharp
using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using {Context}.Domain.Commands;
using {Context}.Domain.Exceptions;
using {Context}.Functions.Models;

namespace {Context}.Functions;

/// <summary>
/// Azure Function HTTP endpoint for {description}.
/// {METHOD} {route}
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// </summary>
public sealed class {Name}Function
{
    private readonly IMediator _mediator;
    private readonly ILogger<{Name}Function> _logger;

    public {Name}Function(IMediator mediator, ILogger<{Name}Function> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("{Name}")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "{method}", Route = "{route}")]
        HttpRequestData request,
        string routeParam)
    {
        _logger.LogInformation("{Name} request received for {Param}: {Value}", routeParam);

        // --- Parse and validate route parameters ---
        if (!Guid.TryParse(routeParam, out var parsedId))
        {
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest,
                "INVALID_ID", "The parameter must be a valid GUID.");
        }

        try
        {
            var command = new {Name}Command { Id = parsedId };
            var result = await _mediator.Send(command);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch ({Context}NotFoundException ex)
        {
            _logger.LogWarning(ex, "Not found: {Id}", parsedId);
            return await CreateErrorResponse(request, HttpStatusCode.NotFound,
                ex.ErrorCode, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Conflict: {Id}", parsedId);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict,
                "CONFLICT", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error: {Id}", parsedId);
            return await CreateErrorResponse(request, HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData request, HttpStatusCode statusCode,
        string errorCode, string message)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new ErrorResponse
        {
            ErrorCode = errorCode,
            Message = message
        });
        return response;
    }
}
```

### Exception-to-HTTP Mapping Table

| Domain Exception | HTTP Status | When |
|-----------------|-------------|------|
| `*NotFoundException` | 404 Not Found | Aggregate does not exist |
| `InvalidOrderException` | 400 Bad Request | Validation failure (business rule) |
| `*CannotBe*Exception` | 409 Conflict | State transition not permitted |
| `InvalidOrderStateException` | 409 Conflict | Wrong aggregate status |
| `DriverTooFarException` | 400 Bad Request | Haversine distance exceeded |
| `DriverNotAvailableException` | 409 Conflict | Driver on active delivery |
| `RestaurantClosedException` | 409 Conflict | Outside operating hours |
| Unhandled `Exception` | 500 Internal Server Error | Unexpected failure |

### Reading JSON Request Body

For POST/PUT endpoints that accept a JSON body:

```csharp
var body = await request.ReadFromJsonAsync<PlaceOrderRequest>();
if (body is null)
{
    return await CreateErrorResponse(request, HttpStatusCode.BadRequest,
        "INVALID_BODY", "Request body is required.");
}
```

### Returning 201 Created

For resource creation endpoints (e.g., PlaceOrder):

```csharp
var response = request.CreateResponse(HttpStatusCode.Created);
response.Headers.Add("Location", $"/orders/{result.OrderId}");
await response.WriteAsJsonAsync(result);
return response;
```

---

## 3. Service Bus Trigger Pattern

### Standard Structure

Every Service Bus function follows this structure:

1. **Deserialize the event from `message.Body`** (catch and complete on failure)
2. **Load the aggregate from the repository** (complete if not found -- eventual consistency)
3. **Call the aggregate's state transition method** (catch invalid transition, log warning, complete)
4. **Save the aggregate** (outbox pattern handles any raised events)
5. **Complete the message** (`messageActions.CompleteMessageAsync`)

### Template

```csharp
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using {Context}.Domain.Events.Consumed;
using {Context}.Domain.Exceptions;
using {Context}.Infrastructure.Repositories;

namespace {Context}.Functions.EventHandlers;

/// <summary>
/// Service Bus trigger function that handles {EventName} events
/// from the {SourceContext} context.
/// Topic: {topic.name}, Subscription: {context}-subscription
/// </summary>
public sealed class Handle{EventName}Function
{
    private readonly I{Aggregate}Repository _repository;
    private readonly ILogger<Handle{EventName}Function> _logger;

    public Handle{EventName}Function(
        I{Aggregate}Repository repository,
        ILogger<Handle{EventName}Function> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [Function("Handle{EventName}")]
    public async Task Run(
        [ServiceBusTrigger(
            "{topic.name}",
            "{context}-subscription",
            Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation(
            "Received {EventName} event, MessageId: {MessageId}",
            message.MessageId);

        // --- 1. Deserialize ---
        {EventName}Event? eventData;
        try
        {
            eventData = JsonSerializer.Deserialize<{EventName}Event>(
                message.Body.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to deserialize {EventName} event, MessageId: {MessageId}. " +
                "Completing message to prevent poison queue.",
                message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        if (eventData?.Payload is null)
        {
            _logger.LogWarning(
                "Null payload in {EventName} event, MessageId: {MessageId}",
                message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        var aggregateId = eventData.Payload.{IdField};

        // --- 2. Load aggregate ---
        var aggregate = await _repository.GetByIdAsync(aggregateId);

        if (aggregate is null)
        {
            _logger.LogWarning(
                "{Aggregate} not found for {EventName} event. " +
                "Id: {Id}, MessageId: {MessageId}. " +
                "Completing message (eventual consistency).",
                aggregateId, message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // --- 3. Apply state transition ---
        try
        {
            aggregate.{TransitionMethod}();
        }
        catch (Invalid{Aggregate}StateException ex)
        {
            _logger.LogWarning(ex,
                "Invalid state transition for {Aggregate} {Id}. " +
                "Current status: {Status}. Completing message (idempotency).",
                aggregateId, aggregate.Status);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // --- 4. Persist ---
        await _repository.SaveAsync(aggregate);

        // --- 5. Complete message ---
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation(
            "{Aggregate} {Id} transitioned to {Status}",
            aggregateId, aggregate.Status);
    }
}
```

### Event Envelope Schema

All cross-context events use a standard envelope:

```csharp
/// <summary>
/// Standard event envelope for Service Bus messages.
/// All cross-context events follow this structure.
/// </summary>
public sealed class {EventName}Event
{
    public string EventType { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string SourceContext { get; set; } = string.Empty;
    public {EventName}Payload? Payload { get; set; }
}

public sealed class {EventName}Payload
{
    public Guid OrderId { get; set; }
    // ... event-specific fields
}
```

### Idempotency Rules

| Scenario | Action | Rationale |
|----------|--------|-----------|
| Aggregate not found | Complete message | Eventual consistency -- event arrived before aggregate created, or after deletion |
| Already in target state | Complete message | Duplicate delivery -- event was already processed |
| Already past target state | Complete message | Out-of-order delivery -- a later event was processed first |
| Invalid source state | Complete message + log warning | Should not happen in normal flow, but completing prevents poison queue |
| Transient failure (Cosmos, network) | Let function throw | Service Bus retries automatically with exponential backoff |

### Dead Letter Queue

Messages are never explicitly dead-lettered by application code. The Service Bus `MaxDeliveryCount` (default: 10) handles truly poisonous messages. All known failure modes are handled by completing the message with a log entry.

---

## 4. Cosmos DB Change Feed Trigger Pattern

### Standard Structure

Used for materializing read-model projections (e.g., the restaurant dashboard).

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using {Context}.Infrastructure.Models;

namespace {Context}.Functions.EventHandlers;

/// <summary>
/// Cosmos DB Change Feed trigger that projects {Aggregate} changes
/// to the {projection} read model container.
/// </summary>
public sealed class {Name}Function
{
    private readonly Container _projectionContainer;
    private readonly ILogger<{Name}Function> _logger;

    public {Name}Function(
        CosmosClient cosmosClient,
        ILogger<{Name}Function> logger)
    {
        _projectionContainer = cosmosClient
            .GetDatabase("swarmeats")
            .GetContainer("{projection-container}");
        _logger = logger;
    }

    [Function("{Name}")]
    public async Task Run(
        [CosmosDBTrigger(
            databaseName: "swarmeats",
            containerName: "{source-container}",
            Connection = "CosmosDBConnection",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<{DocumentType}> changes,
        FunctionContext context)
    {
        if (changes is null || changes.Count == 0) return;

        _logger.LogInformation(
            "Processing {Count} changes from {Container}",
            changes.Count, "{source-container}");

        foreach (var document in changes)
        {
            try
            {
                var projection = MapToProjection(document);

                await _projectionContainer.UpsertItemAsync(
                    projection,
                    new PartitionKey(projection.PartitionKey));
            }
            catch (Exception ex)
            {
                // Log but don't throw -- change feed will retry from checkpoint
                // on the next invocation if we throw
                _logger.LogError(ex,
                    "Failed to project document {Id}", document.Id);
            }
        }
    }

    private static {ProjectionType} MapToProjection({DocumentType} document)
    {
        return new {ProjectionType}
        {
            Id = document.Id,
            PartitionKey = document.PartitionKey,
            // ... map fields for the read model
            ProjectedAt = DateTimeOffset.UtcNow
        };
    }
}
```

### Change Feed Guidelines

| Guideline | Detail |
|-----------|--------|
| Lease container | Shared `leases` container per database, `CreateLeaseContainerIfNotExists = true` |
| Error handling | Log and continue -- do not throw for individual document failures |
| Idempotency | Upsert projections (not insert) since change feed may redeliver |
| TTL | Set TTL on projections if they are transient views (e.g., 30 days for dashboard) |
| Partition key | Projection partition key should match the query pattern (e.g., `restaurantId` for per-restaurant dashboard) |

---

## 5. Timer Trigger Pattern

### Standard Structure

Used for periodic background tasks (e.g., monitoring unassigned deliveries).

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace {Context}.Functions.EventHandlers;

/// <summary>
/// Timer trigger that runs every {interval} to {purpose}.
/// </summary>
public sealed class {Name}Function
{
    private readonly I{Repository} _repository;
    private readonly ILogger<{Name}Function> _logger;

    public {Name}Function(
        I{Repository} repository,
        ILogger<{Name}Function> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [Function("{Name}")]
    public async Task Run(
        [TimerTrigger("{cron-expression}")] TimerInfo timerInfo,
        FunctionContext context)
    {
        _logger.LogInformation(
            "{Name} triggered at {Time}. Past due: {IsPastDue}",
            DateTimeOffset.UtcNow, timerInfo.IsPastDue);

        var items = await _repository.Get{Criteria}Async();

        if (items.Count == 0)
        {
            _logger.LogDebug("No items matching criteria. Nothing to do.");
            return;
        }

        _logger.LogWarning(
            "Found {Count} items matching monitoring criteria",
            items.Count);

        foreach (var item in items)
        {
            _logger.LogWarning(
                "ALERT: {Item} {Id} has been {Condition} for {Duration}",
                item.Id, /* calculated duration */);
        }
    }
}
```

### Timer Cron Expressions

| Expression | Frequency | Use Case |
|-----------|-----------|----------|
| `*/30 * * * * *` | Every 30 seconds | Unassigned delivery monitor |
| `0 */5 * * * *` | Every 5 minutes | Outbox processor (if not using change feed) |
| `0 0 * * * *` | Every hour | Stale order cleanup |
| `0 0 0 * * *` | Daily at midnight | Report generation |

### Timer Guidelines

| Guideline | Detail |
|-----------|--------|
| Singleton | Timer functions run as a single instance across all scaled-out replicas |
| IsPastDue | Check `timerInfo.IsPastDue` -- if true, the function missed its schedule (e.g., during deployment). Log but process normally. |
| Idempotent | Timer functions must be idempotent since they can fire slightly early/late or overlap during deployments |
| No state | Timer functions should query for current state each invocation, not carry state between runs |

---

## 6. Cosmos DB Repository Pattern

### Transactional Batch with Outbox

Every write operation uses a Cosmos DB transactional batch to atomically persist:
1. The aggregate document (upsert with ETag check)
2. One outbox message per domain event raised

```csharp
public async Task SaveAsync({Aggregate} aggregate, CancellationToken ct = default)
{
    var partitionKey = new PartitionKey(aggregate.{PartitionKeyProperty}.ToString());
    var batch = _container.CreateTransactionalBatch(partitionKey);

    // Upsert aggregate with optimistic concurrency
    batch.UpsertItem(aggregate, new TransactionalBatchItemRequestOptions
    {
        IfMatchEtag = aggregate.ETag
    });

    // Persist domain events to outbox in same transaction
    foreach (var domainEvent in aggregate.DomainEvents)
    {
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid().ToString(),
            PartitionKey = aggregate.{PartitionKeyProperty}.ToString(),
            EventType = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            CreatedAt = DateTimeOffset.UtcNow,
            Processed = false
        };

        batch.CreateItem(outboxMessage);
    }

    var response = await batch.ExecuteAsync(ct);

    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Transactional batch failed with status {response.StatusCode}.");
    }

    aggregate.ClearDomainEvents();
}
```

### Partition Key Strategy

| Container | Partition Key | Rationale |
|-----------|--------------|-----------|
| `orders` | `/customerId` | Queries by customer use partition-scoped reads; cancellation/status updates use cross-partition point read by orderId |
| `menus` | `/restaurantId` | One menu per restaurant -- always accessed by restaurant |
| `restaurant-orders` | `/restaurantId` | Dashboard and order list queries are always per-restaurant |
| `deliveries` | `/driverId` | Driver queries (active delivery check, location updates) are per-driver; unassigned deliveries use sentinel key `"unassigned"` |
| Outbox containers | Same as parent | Must share partition key for transactional batch atomicity |

### Cross-Partition Queries

Avoid cross-partition queries in hot paths. Where unavoidable (e.g., looking up an order by `orderId` when `customerId` is unknown):

```csharp
var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
    .WithParameter("@id", id.ToString());

var iterator = _container.GetItemQueryIterator<T>(query);
```

This fans out across partitions. Acceptable for low-frequency operations (cancel, get-by-id) but not for high-throughput reads.

### Optimistic Concurrency

All aggregates carry an `ETag` property. The repository sets `IfMatchEtag` on upsert. If another writer modified the document since it was read, Cosmos returns `412 Precondition Failed` and the operation fails. The caller can retry with a fresh read.

```csharp
public abstract class AggregateRoot<TId>
{
    public TId Id { get; protected set; } = default!;
    public string? ETag { get; set; }
    // ...
}
```

---

## 7. MediatR Pipeline

### Registration

```csharp
services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

### Command Pattern

Commands mutate state and may raise domain events:

```csharp
// Command definition
public sealed record CancelOrderCommand : IRequest<CancelOrderResult>
{
    public required Guid OrderId { get; init; }
}

// Result DTO
public sealed record CancelOrderResult
{
    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset CancelledAt { get; init; }
}
```

### Query Pattern

Queries are read-only and produce no side effects:

```csharp
public sealed record GetOrderQuery : IRequest<GetOrderResponse>
{
    public required Guid OrderId { get; init; }
}
```

### Handler Contract

All handlers follow load-delegate-save:

```csharp
public sealed class {Name}Handler : IRequestHandler<{Command}, {Result}>
{
    private readonly I{Aggregate}Repository _repository;

    public async Task<{Result}> Handle({Command} request, CancellationToken ct)
    {
        var aggregate = await _repository.GetByIdAsync(request.Id, ct);
        if (aggregate is null) throw new {Aggregate}NotFoundException(request.Id);

        aggregate.{Method}(request.Param);  // ALL logic here

        await _repository.SaveAsync(aggregate, ct);

        return new {Result} { /* map from aggregate */ };
    }
}
```

---

## 8. Error Response Contract

All HTTP endpoints return errors in a consistent format:

```json
{
    "errorCode": "ORDER_NOT_FOUND",
    "message": "Order with ID '3fa85f64-5717-4562-b3fc-2c963f66afa6' was not found."
}
```

### ErrorResponse DTO

Each bounded context has its own copy at `Functions/Models/ErrorResponse.cs`:

```csharp
namespace {Context}.Functions.Models;

/// <summary>
/// Standard error response DTO for API error payloads.
/// </summary>
internal sealed class ErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
```

### Error Code Naming Convention

| Pattern | Example | When |
|---------|---------|------|
| `{ENTITY}_NOT_FOUND` | `ORDER_NOT_FOUND` | Aggregate does not exist |
| `{ENTITY}_CANNOT_{ACTION}` | `ORDER_CANNOT_CANCEL` | State transition not allowed |
| `{ENTITY}_INVALID_TRANSITION` | `ORDER_INVALID_TRANSITION` | Generic invalid state change |
| `{RULE}_VIOLATION` | `ORDER_MINIMUM_NOT_MET` | Business rule violated |
| `INVALID_{FIELD}` | `INVALID_ORDER_ID` | Input validation failure |
| `INTERNAL_ERROR` | `INTERNAL_ERROR` | Unhandled exception (no details leaked) |

---

## 9. Dependency Injection Summary

### Singleton Services

| Service | Rationale |
|---------|-----------|
| `CosmosClient` | Thread-safe, connection pooling, expensive to create |

### Scoped Services

| Service | Rationale |
|---------|-----------|
| `IOrderRepository` / `CosmosOrderRepository` | One per function invocation, holds container references |
| `IMenuRepository` / `CosmosMenuRepository` | One per function invocation |
| `IDeliveryRepository` / `CosmosDeliveryRepository` | One per function invocation |
| `IDriverRepository` / `CosmosDriverRepository` | One per function invocation |

### Transient Services

| Service | Rationale |
|---------|-----------|
| MediatR handlers | Stateless, created per dispatch |

---

## 10. Configuration

### Required Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `CosmosDBConnection` | Cosmos DB connection string | `AccountEndpoint=https://...;AccountKey=...` |
| `CosmosDBDatabase` | Database name | `swarmeats` |
| `ServiceBusConnection` | Service Bus connection string | `Endpoint=sb://...;SharedAccessKeyName=...` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | App Insights telemetry | `InstrumentationKey=...` |

### local.settings.json (Development)

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "CosmosDBConnection": "AccountEndpoint=https://localhost:8081;AccountKey=...",
        "CosmosDBDatabase": "swarmeats",
        "ServiceBusConnection": "Endpoint=sb://localhost;SharedAccessKeyName=..."
    }
}
```

### Test Environment Variables

Per-story test environments use isolated containers and topics:

| Variable | Purpose |
|----------|---------|
| `TEST_COSMOS_ENDPOINT` | Test Cosmos emulator endpoint |
| `TEST_COSMOS_DATABASE` | Test database name |
| `TEST_COSMOS_CONTAINER` | Story-specific container name |
| `TEST_SERVICEBUS_CONNECTION` | Test Service Bus namespace |
| `TEST_SERVICEBUS_TOPIC` | Story-specific topic name |
| `TEST_FUNCTION_SLOT_URL` | Deployment slot URL for integration tests |
| `TEST_STORY_ID` | Story identifier for test isolation |
