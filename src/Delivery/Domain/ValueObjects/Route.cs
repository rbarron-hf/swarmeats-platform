namespace Delivery.Domain.ValueObjects;

/// <summary>
/// Value object representing the delivery route from restaurant (pickup)
/// to customer (dropoff). Contains the full address details for both endpoints.
/// Immutable by design as a C# record.
/// </summary>
public record Route(
    Location Pickup,
    Location Dropoff);
