namespace Delivery.Domain.Exceptions;

/// <summary>
/// Thrown when a driver attempts an action on a delivery they are not assigned to.
/// Maps to HTTP 403 with error code DELIVERY_WRONG_DRIVER.
/// </summary>
public sealed class WrongDriverException : Exception
{
    public Guid DeliveryId { get; }
    public Guid RequestedDriverId { get; }
    public string ErrorCode => "DELIVERY_WRONG_DRIVER";

    public WrongDriverException(Guid deliveryId, Guid requestedDriverId)
        : base($"Driver '{requestedDriverId}' is not assigned to delivery '{deliveryId}'.")
    {
        DeliveryId = deliveryId;
        RequestedDriverId = requestedDriverId;
    }

    public WrongDriverException(Guid deliveryId, Guid requestedDriverId, Exception innerException)
        : base($"Driver '{requestedDriverId}' is not assigned to delivery '{deliveryId}'.", innerException)
    {
        DeliveryId = deliveryId;
        RequestedDriverId = requestedDriverId;
    }
}
