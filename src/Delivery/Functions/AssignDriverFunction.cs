using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Delivery.Domain.Commands;
using Delivery.Domain.Exceptions;
using Delivery.Functions.Models;

namespace Delivery.Functions;

/// <summary>
/// Azure Function HTTP endpoint for assigning a driver to a delivery.
/// POST /deliveries/{deliveryId}/assign
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// </summary>
public sealed class AssignDriverFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<AssignDriverFunction> _logger;

    public AssignDriverFunction(IMediator mediator, ILogger<AssignDriverFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("AssignDriver")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "deliveries/{deliveryId}/assign")]
        HttpRequestData request,
        string deliveryId)
    {
        _logger.LogInformation("Assign driver request received for deliveryId: {DeliveryId}", deliveryId);

        if (!Guid.TryParse(deliveryId, out var parsedDeliveryId))
        {
            _logger.LogWarning("Invalid deliveryId format: {DeliveryId}", deliveryId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_DELIVERY_ID",
                "The deliveryId must be a valid GUID.");
        }

        try
        {
            var requestBody = await request.ReadFromJsonAsync<AssignDriverRequestBody>();
            if (requestBody is null)
            {
                return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_REQUEST",
                    "Request body is required.");
            }

            var command = new AssignDriverCommand
            {
                DeliveryId = parsedDeliveryId,
                DriverId = requestBody.DriverId,
                DriverLatitude = requestBody.DriverLocation.Latitude,
                DriverLongitude = requestBody.DriverLocation.Longitude
            };

            var result = await _mediator.Send(command);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (DeliveryNotFoundException ex)
        {
            _logger.LogWarning(ex, "Delivery not found: {DeliveryId}", parsedDeliveryId);
            return await CreateErrorResponse(request, HttpStatusCode.NotFound, ex.ErrorCode, ex.Message);
        }
        catch (InvalidDeliveryStateException ex)
        {
            _logger.LogWarning(ex, "Invalid delivery state for assignment: {DeliveryId}, status: {Status}",
                parsedDeliveryId, ex.CurrentStatus);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, ex.ErrorCode, ex.Message);
        }
        catch (DriverNotAvailableException ex)
        {
            _logger.LogWarning(ex, "Driver not available: {DriverId}", ex.DriverId);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, ex.ErrorCode, ex.Message);
        }
        catch (DriverTooFarException ex)
        {
            _logger.LogWarning(ex, "Driver too far: {DriverId}, distance: {DistanceKm}km", ex.DriverId, ex.DistanceKm);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error assigning driver to delivery: {DeliveryId}", parsedDeliveryId);
            return await CreateErrorResponse(request, HttpStatusCode.InternalServerError, "INTERNAL_ERROR",
                "An unexpected error occurred.");
        }
    }

    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string errorCode,
        string message)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new ErrorResponse
        {
            ErrorCode = errorCode,
            Message = message
        });
        return response;
    }
}

/// <summary>
/// Request body for the assign driver endpoint.
/// </summary>
internal sealed class AssignDriverRequestBody
{
    public Guid DriverId { get; set; }
    public AssignDriverLocationBody DriverLocation { get; set; } = new();
}

/// <summary>
/// Driver location within the assign driver request body.
/// </summary>
internal sealed class AssignDriverLocationBody
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
