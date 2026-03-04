using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using {ContextName}.Domain.Events.Consumed;
using {ContextName}.Domain.Exceptions;
using {ContextName}.Infrastructure.Repositories;

namespace {ContextName}.Functions.EventHandlers;

/// <summary>
/// Service Bus trigger function that handles {EventName} events
/// from the {SourceContext} context.
///
/// Topic: {topic.name}
/// Subscription: {context}-subscription
/// Story: {STORY_ID}
///
/// Idempotency: checks current aggregate state before applying transition.
/// If already in target state (or later), logs warning and completes message.
/// </summary>
public sealed class Handle{EventName}Function
{
    private readonly I{Aggregate}Repository _repository;
    private readonly ILogger<Handle{EventName}Function> _logger;

    public Handle{EventName}Function(
        I{Aggregate}Repository repository,
        ILogger<Handle{EventName}Function> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            "Received {EventName} event. MessageId: {MessageId}",
            message.MessageId);

        // ── 1. Deserialize ──
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
                "Failed to deserialize {EventName}. MessageId: {MessageId}. " +
                "Completing to prevent poison queue.",
                message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        if (eventData?.Payload is null)
        {
            _logger.LogWarning(
                "Null payload in {EventName}. MessageId: {MessageId}",
                message.MessageId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        var aggregateId = eventData.Payload.{IdField};

        // ── 2. Load aggregate ──
        var aggregate = await _repository.GetByIdAsync(aggregateId);

        if (aggregate is null)
        {
            _logger.LogWarning(
                "{Aggregate} not found for {EventName}. Id: {Id}. " +
                "Completing message (eventual consistency).",
                aggregateId);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // ── 3. Apply state transition ──
        try
        {
            aggregate.{TransitionMethod}(/* pass event fields */);
        }
        catch (Invalid{Aggregate}StateException ex)
        {
            _logger.LogWarning(ex,
                "Invalid transition for {Aggregate} {Id}. " +
                "Current status: {Status}. Completing (idempotency).",
                aggregateId, aggregate.Status);
            await messageActions.CompleteMessageAsync(message);
            return;
        }

        // ── 4. Persist ──
        await _repository.SaveAsync(aggregate);

        // ── 5. Complete message ──
        await messageActions.CompleteMessageAsync(message);

        _logger.LogInformation(
            "{Aggregate} {Id} transitioned to {Status}",
            aggregateId, aggregate.Status);
    }
}
