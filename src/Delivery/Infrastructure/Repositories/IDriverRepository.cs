namespace Delivery.Infrastructure.Repositories;

/// <summary>
/// Repository interface for checking driver availability.
/// Queries the deliveries container to determine if a driver already has an active delivery.
/// </summary>
public interface IDriverRepository
{
    /// <summary>
    /// Checks whether the specified driver currently has an active delivery
    /// (a delivery in DriverAssigned or PickedUp status).
    /// Enforces business rule DLV-R01.
    /// </summary>
    /// <param name="driverId">The driver identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the driver has an active delivery; false if the driver is available.</returns>
    Task<bool> HasActiveDeliveryAsync(Guid driverId, CancellationToken cancellationToken = default);
}
