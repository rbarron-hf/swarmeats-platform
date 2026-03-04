using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Delivery.Infrastructure.Repositories;

namespace Delivery.Functions.EventHandlers;

/// <summary>
/// Azure Function Timer trigger that monitors for unassigned deliveries.
/// Runs every 30 seconds and queries for deliveries in AwaitingDriver status
/// that have been waiting longer than 5 minutes.
/// DLV-008: Unassigned Delivery Monitor.
/// This function does NOT auto-assign drivers -- it is for operational monitoring only.
/// </summary>
public sealed class UnassignedDeliveryMonitorFunction
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ILogger<UnassignedDeliveryMonitorFunction> _logger;

    /// <summary>
    /// Threshold in minutes. Deliveries in AwaitingDriver status older than this
    /// are considered overdue and trigger a warning log.
    /// </summary>
    private const int OverdueThresholdMinutes = 5;

    public UnassignedDeliveryMonitorFunction(
        IDeliveryRepository deliveryRepository,
        ILogger<UnassignedDeliveryMonitorFunction> logger)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UnassignedDeliveryMonitor")]
    public async Task Run(
        [TimerTrigger("*/30 * * * * *")] TimerInfo timerInfo,
        FunctionContext context)
    {
        _logger.LogInformation("Unassigned delivery monitor triggered at: {Timestamp}", DateTimeOffset.UtcNow);

        try
        {
            var overdueDeliveries = await _deliveryRepository.GetOverdueUnassignedDeliveriesAsync(OverdueThresholdMinutes);

            if (overdueDeliveries.Count == 0)
            {
                _logger.LogDebug("No overdue unassigned deliveries found.");
                return;
            }

            _logger.LogWarning(
                "Found {Count} overdue unassigned deliveries (waiting longer than {Threshold} minutes).",
                overdueDeliveries.Count, OverdueThresholdMinutes);

            foreach (var delivery in overdueDeliveries)
            {
                var waitingMinutes = (DateTimeOffset.UtcNow - delivery.CreatedAt).TotalMinutes;

                _logger.LogWarning(
                    "Overdue unassigned delivery. DeliveryId: {DeliveryId}, OrderId: {OrderId}, " +
                    "CreatedAt: {CreatedAt}, WaitingMinutes: {WaitingMinutes:F1}",
                    delivery.Id, delivery.OrderId, delivery.CreatedAt, waitingMinutes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during unassigned delivery monitoring check.");
            // Do not rethrow -- timer functions should not fail permanently
        }
    }
}
