namespace Delivery.Domain.Exceptions;

/// <summary>
/// Thrown when a delivery with the specified identifier cannot be found.
/// Maps to HTTP 404 with error code DELIVERY_NOT_FOUND.
/// </summary>
public sealed class DeliveryNotFoundException : Exception
{
    public Guid DeliveryId { get; }
    public string ErrorCode => "DELIVERY_NOT_FOUND";

    public DeliveryNotFoundException(Guid deliveryId)
        : base($"Delivery with ID '{deliveryId}' was not found.")
    {
        DeliveryId = deliveryId;
    }

    public DeliveryNotFoundException(Guid deliveryId, Exception innerException)
        : base($"Delivery with ID '{deliveryId}' was not found.", innerException)
    {
        DeliveryId = deliveryId;
    }
}
