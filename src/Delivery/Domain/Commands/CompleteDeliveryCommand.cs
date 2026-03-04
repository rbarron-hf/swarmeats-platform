using MediatR;

namespace Delivery.Domain.Commands;

/// <summary>
/// Command to complete a delivery to the customer.
/// Dispatched from CompleteDeliveryFunction to the handler via MediatR.
/// </summary>
public sealed record CompleteDeliveryCommand : IRequest<CompleteDeliveryResult>
{
    /// <summary>
    /// Identifier of the delivery to complete.
    /// </summary>
    public required Guid DeliveryId { get; init; }

    /// <summary>
    /// Identifier of the driver confirming delivery completion.
    /// </summary>
    public required Guid DriverId { get; init; }
}

/// <summary>
/// Result returned on successful delivery completion.
/// </summary>
public sealed record CompleteDeliveryResult
{
    public required Guid DeliveryId { get; init; }
    public required Guid OrderId { get; init; }
    public required string Status { get; init; }
    public required Guid DriverId { get; init; }
    public required DateTimeOffset DeliveredAt { get; init; }
    public required int TotalDeliveryMinutes { get; init; }
    public required bool SlaBreached { get; init; }
}
