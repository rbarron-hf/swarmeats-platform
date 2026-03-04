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
/// Azure Function HTTP endpoint for updating driver location during delivery.
/// PUT /deliveries/{deliveryId}/location
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// </summary>
public sealed class UpdateDriverLocationFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateDriverLocationFunction> _logger;

    public UpdateDriverLocationFunction(IMediator mediator, ILogger<UpdateDriverLocationFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateDriverLocation")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "deliveries/{deliveryId}/location")]
        HttpRequestData request,
        string deliveryId)
    {
        _logger.LogInformation("Update driver location request received for deliveryId: {DeliveryId}", deliveryId);

        if (!Guid.TryParse(deliveryId, out var parsedDeliveryId))
        {
            _logger.LogWarning("Invalid deliveryId format: {DeliveryId}", deliveryId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_DELIVERY_ID",
                "The deliveryId must be a valid GUID.");
        }

        try
        {
            var requestBody = await request.ReadFromJsonAsync<UpdateDriverLocationRequestBody>();
            if (requestBody is null)
            {
                return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_REQUEST",
                    "Request body is required.");
            }

            var command = new UpdateDriverLocationCommand
            {
                DeliveryId = parsedDeliveryId,
                DriverId = requestBody.DriverId,
                Latitude = requestBody.Latitude,
                Longitude = requestBody.Longitude
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
            _logger.LogWarning(ex, "Delivery not active for location update: {DeliveryId}, status: {Status}",
                parsedDeliveryId, ex.CurrentStatus);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, ex.ErrorCode, ex.Message);
        }
        catch (WrongDriverException ex)
        {
            _logger.LogWarning(ex, "Wrong driver for delivery: {DeliveryId}, driver: {DriverId}",
                ex.DeliveryId, ex.RequestedDriverId);
            return await CreateErrorResponse(request, HttpStatusCode.Forbidden, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating driver location for delivery: {DeliveryId}", parsedDeliveryId);
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
/// Request body for the update driver location endpoint.
/// </summary>
internal sealed class UpdateDriverLocationRequestBody
{
    public Guid DriverId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
