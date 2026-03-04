namespace Delivery.Domain.ValueObjects;

/// <summary>
/// Value object representing a driver's current geographic position and the timestamp
/// when that position was recorded. Updated via location pings (DLV-003).
/// Immutable by design as a C# record.
/// </summary>
public record DriverLocation(
    double Latitude,
    double Longitude,
    DateTimeOffset UpdatedAt);
