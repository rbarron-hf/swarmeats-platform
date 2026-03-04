namespace Delivery.Domain.Exceptions;

/// <summary>
/// Thrown when a driver cannot be assigned because they are more than 5km from the restaurant.
/// Enforces business rule DLV-R02 (Haversine distance check).
/// Maps to HTTP 400 with error code DRIVER_TOO_FAR.
/// </summary>
public sealed class DriverTooFarException : Exception
{
    public Guid DriverId { get; }
    public double DistanceKm { get; }
    public string ErrorCode => "DRIVER_TOO_FAR";

    public DriverTooFarException(Guid driverId, double distanceKm)
        : base($"Driver '{driverId}' is {distanceKm:F2}km from the restaurant. Maximum allowed distance is 5km.")
    {
        DriverId = driverId;
        DistanceKm = distanceKm;
    }

    public DriverTooFarException(Guid driverId, double distanceKm, Exception innerException)
        : base($"Driver '{driverId}' is {distanceKm:F2}km from the restaurant. Maximum allowed distance is 5km.", innerException)
    {
        DriverId = driverId;
        DistanceKm = distanceKm;
    }
}
