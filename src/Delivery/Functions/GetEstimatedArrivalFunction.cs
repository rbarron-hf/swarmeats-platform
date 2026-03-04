using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Delivery.Domain.Exceptions;
using Delivery.Domain.Queries;
using Delivery.Functions.Models;

namespace Delivery.Functions;

/// <summary>
/// Azure Function HTTP endpoint for retrieving estimated arrival time.
/// GET /deliveries/{deliveryId}/eta
/// Isolated worker model. Delegates to MediatR pipeline.
/// </summary>
public sealed class GetEstimatedArrivalFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetEstimatedArrivalFunction> _logger;

    public GetEstimatedArrivalFunction(IMediator mediator, ILogger<GetEstimatedArrivalFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetEstimatedArrival")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "deliveries/{deliveryId}/eta")]
        HttpRequestData request,
        string deliveryId)
    {
        _logger.LogInformation("Get estimated arrival request received for deliveryId: {DeliveryId}", deliveryId);

        if (!Guid.TryParse(deliveryId, out var parsedDeliveryId))
        {
            _logger.LogWarning("Invalid deliveryId format: {DeliveryId}", deliveryId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_DELIVERY_ID",
                "The deliveryId must be a valid GUID.");
        }

        try
        {
            var query = new GetEstimatedArrivalQuery { DeliveryId = parsedDeliveryId };
            var result = await _mediator.Send(query);

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
            _logger.LogWarning(ex, "No driver assigned for delivery: {DeliveryId}", parsedDeliveryId);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting estimated arrival for delivery: {DeliveryId}", parsedDeliveryId);
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
