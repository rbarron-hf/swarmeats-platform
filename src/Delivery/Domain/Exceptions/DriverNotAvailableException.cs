namespace Delivery.Domain.Exceptions;

/// <summary>
/// Thrown when a driver cannot be assigned because they already have an active delivery
/// (in DriverAssigned or PickedUp status). Enforces business rule DLV-R01.
/// Maps to HTTP 409 with error code DRIVER_NOT_AVAILABLE.
/// </summary>
public sealed class DriverNotAvailableException : Exception
{
    public Guid DriverId { get; }
    public string ErrorCode => "DRIVER_NOT_AVAILABLE";

    public DriverNotAvailableException(Guid driverId)
        : base($"Driver '{driverId}' is not available. They already have an active delivery.")
    {
        DriverId = driverId;
    }

    public DriverNotAvailableException(Guid driverId, Exception innerException)
        : base($"Driver '{driverId}' is not available. They already have an active delivery.", innerException)
    {
        DriverId = driverId;
    }
}
