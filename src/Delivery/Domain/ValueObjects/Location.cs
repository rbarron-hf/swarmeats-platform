namespace Delivery.Domain.ValueObjects;

/// <summary>
/// Value object representing a geographic location with address details.
/// Used for both restaurant (pickup) and customer (dropoff) addresses.
/// Immutable by design as a C# record.
/// </summary>
public record Location(
    string Street,
    string City,
    string Postcode,
    double Latitude,
    double Longitude);
