namespace Delivery.Domain.ValueObjects;

/// <summary>
/// Value object representing the estimated arrival time at the customer's delivery address.
/// Updated on driver assignment (DLV-002) and each driver location ping (DLV-003).
/// Used for 45-minute SLA monitoring (DLV-R03).
/// Immutable by design as a C# record.
/// </summary>
public record EstimatedArrival(
    DateTimeOffset EstimatedAt,
    int EstimatedMinutes);
