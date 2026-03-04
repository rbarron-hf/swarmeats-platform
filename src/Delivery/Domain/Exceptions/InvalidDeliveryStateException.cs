using Delivery.Domain.ValueObjects;

namespace Delivery.Domain.Exceptions;

/// <summary>
/// Thrown when an invalid state transition is attempted on the Delivery aggregate.
/// Maps to HTTP 409 with a context-specific error code describing the invalid transition.
/// </summary>
public sealed class InvalidDeliveryStateException : Exception
{
    public Guid DeliveryId { get; }
    public DeliveryStatus CurrentStatus { get; }
    public string ErrorCode { get; }

    public InvalidDeliveryStateException(Guid deliveryId, DeliveryStatus currentStatus, string errorCode, string message)
        : base(message)
    {
        DeliveryId = deliveryId;
        CurrentStatus = currentStatus;
        ErrorCode = errorCode;
    }

    public InvalidDeliveryStateException(Guid deliveryId, DeliveryStatus currentStatus, string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        DeliveryId = deliveryId;
        CurrentStatus = currentStatus;
        ErrorCode = errorCode;
    }
}
