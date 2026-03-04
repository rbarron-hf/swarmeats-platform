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
/// Azure Function HTTP endpoint for accepting a restaurant order.
/// POST /restaurants/{restaurantId}/orders/{orderId}/accept
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// RST-004.
/// </summary>
public sealed class AcceptOrderFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<AcceptOrderFunction> _logger;

    public AcceptOrderFunction(IMediator mediator, ILogger<AcceptOrderFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("AcceptOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "restaurants/{restaurantId}/orders/{orderId}/accept")]
        HttpRequestData request,
        string restaurantId,
        string orderId)
    {
        _logger.LogInformation("Accept order request received for restaurantId: {RestaurantId}, orderId: {OrderId}",
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
        var body = await request.ReadFromJsonAsync<AcceptOrderRequestBody>();
        if (body is null || body.EstimatedPrepMinutes == 0)
        {
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "RESTAURANT_INVALID_PREP_TIME",
                "Request body must include estimatedPrepMinutes.");
        }

        try
        {
            // --- Dispatch the command via MediatR ---
            var command = new AcceptOrderCommand
            {
                OrderId = parsedOrderId,
                RestaurantId = parsedRestaurantId,
                EstimatedPrepMinutes = body.EstimatedPrepMinutes
            };
            var result = await _mediator.Send(command);

            // --- Return 200 with the updated order ---
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (RestaurantOrderNotFoundException ex)
        {
            _logger.LogWarning(ex, "Order not found: {OrderId} for restaurant: {RestaurantId}",
                parsedOrderId, parsedRestaurantId);
            return await CreateErrorResponse(request, HttpStatusCode.NotFound, ex.ErrorCode, ex.Message);
        }
        catch (InvalidOrderStateException ex)
        {
            _logger.LogWarning(ex, "Order state invalid for accept: {OrderId}, current status: {Status}",
                parsedOrderId, ex.CurrentStatus);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, ex.ErrorCode, ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Invalid prep time for order: {OrderId}", parsedOrderId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "RESTAURANT_INVALID_PREP_TIME",
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error accepting order: {OrderId}", parsedOrderId);
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
/// Request body for the AcceptOrder endpoint.
/// </summary>
internal sealed class AcceptOrderRequestBody
{
    public int EstimatedPrepMinutes { get; set; }
}
