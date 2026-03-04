using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Restaurant.Domain.Commands;
using Restaurant.Domain.Exceptions;
using Restaurant.Functions.Models;

namespace Restaurant.Functions;

/// <summary>
/// Azure Function HTTP endpoint for updating a restaurant order's preparation status.
/// PUT /restaurants/{restaurantId}/orders/{orderId}/status
/// Handles both MarkPreparing (RST-006) and MarkReadyForPickup (RST-007) based on the
/// requested status value in the request body.
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// </summary>
public sealed class UpdatePreparationStatusFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<UpdatePreparationStatusFunction> _logger;

    public UpdatePreparationStatusFunction(IMediator mediator, ILogger<UpdatePreparationStatusFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdatePreparationStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "restaurants/{restaurantId}/orders/{orderId}/status")]
        HttpRequestData request,
        string restaurantId,
        string orderId)
    {
        _logger.LogInformation("Update preparation status request received for restaurantId: {RestaurantId}, orderId: {OrderId}",
            restaurantId, orderId);

        // --- Parse and validate route parameters ---
        if (!Guid.TryParse(restaurantId, out var parsedRestaurantId))
        {
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_RESTAURANT_ID",
                "The restaurantId must be a valid GUID.");
        }

        if (!Guid.TryParse(orderId, out var parsedOrderId))
        {
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_ORDER_ID",
                "The orderId must be a valid GUID.");
        }

        // --- Parse request body ---
        var body = await request.ReadFromJsonAsync<UpdateStatusRequestBody>();
        if (body is null || string.IsNullOrWhiteSpace(body.Status))
        {
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "RESTAURANT_INVALID_TRANSITION",
                "Request body must include a status value.");
        }

        try
        {
            // --- Dispatch the appropriate command based on the requested status ---
            switch (body.Status.ToLowerInvariant())
            {
                case "preparing":
                {
                    var command = new MarkPreparingCommand
                    {
                        OrderId = parsedOrderId,
                        RestaurantId = parsedRestaurantId
                    };
                    var result = await _mediator.Send(command);

                    var response = request.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(result);
                    return response;
                }

                case "readyforpickup":
                {
                    var command = new MarkReadyForPickupCommand
                    {
                        OrderId = parsedOrderId,
                        RestaurantId = parsedRestaurantId
                    };
                    var result = await _mediator.Send(command);

                    var response = request.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(result);
                    return response;
                }

                default:
                    return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "RESTAURANT_INVALID_TRANSITION",
                        $"The status '{body.Status}' is not valid for this endpoint. Allowed values: Preparing, ReadyForPickup.");
            }
        }
        catch (RestaurantOrderNotFoundException ex)
        {
            _logger.LogWarning(ex, "Order not found: {OrderId} for restaurant: {RestaurantId}",
                parsedOrderId, parsedRestaurantId);
            return await CreateErrorResponse(request, HttpStatusCode.NotFound, ex.ErrorCode, ex.Message);
        }
        catch (InvalidOrderStateException ex)
        {
            _logger.LogWarning(ex, "Invalid status transition for order: {OrderId}, current status: {Status}",
                parsedOrderId, ex.CurrentStatus);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating status for order: {OrderId}", parsedOrderId);
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
/// Request body for the UpdatePreparationStatus endpoint.
/// </summary>
internal sealed class UpdateStatusRequestBody
{
    public string Status { get; set; } = string.Empty;
}
