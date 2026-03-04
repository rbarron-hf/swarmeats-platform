using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using {ContextName}.Infrastructure.Repositories;

namespace {ContextName}.Functions.EventHandlers;

/// <summary>
/// Timer trigger function that runs every {interval} to {purpose}.
///
/// Cron: {cron-expression}
/// Story: {STORY_ID}
///
/// Timer functions run as singletons (one instance across all replicas).
/// Must be idempotent — queries current state each invocation.
/// </summary>
public sealed class {FunctionName}
{
    private readonly I{Aggregate}Repository _repository;
    private readonly ILogger<{FunctionName}> _logger;

    public {FunctionName}(
        I{Aggregate}Repository repository,
        ILogger<{FunctionName}> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("{FunctionName}")]
    public async Task Run(
        [TimerTrigger("{cron-expression}")] TimerInfo timerInfo,
        FunctionContext context)
    {
        _logger.LogInformation(
            "{FunctionName} triggered at {Time}. Past due: {IsPastDue}",
            DateTimeOffset.UtcNow, timerInfo.IsPastDue);

        // ── Query for items matching monitoring criteria ──
        var items = await _repository.Get{Criteria}Async();

        if (items.Count == 0)
        {
            _logger.LogDebug("No items matching criteria. Nothing to do.");
            return;
        }

        _logger.LogWarning(
            "MONITORING: Found {Count} items matching alert criteria.",
            items.Count);

        foreach (var item in items)
        {
            _logger.LogWarning(
                "ALERT: {Entity} {Id} — {AlertDescription}",
                item.Id /* , additional context */);
        }
    }
}
