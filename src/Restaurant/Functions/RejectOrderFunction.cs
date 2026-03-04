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
/// Azure Function HTTP endpoint for rejecting a restaurant order.
/// POST /restaurants/{restaurantId}/orders/{orderId}/reject
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// RST-005.
/// </summary>
public sealed class RejectOrderFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<RejectOrderFunction> _logger;

    public RejectOrderFunction(IMediator mediator, ILogger<RejectOrderFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("RejectOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "restaurants/{restaurantId}/orders/{orderId}/reject")]
        HttpRequestData request,
        string restaurantId,
        string orderId)
    {
        _logger.LogInformation("Reject order request received for restaurantId: {RestaurantId}, orderId: {OrderId}",
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
        var body = await request.ReadFromJsonAsync<RejectOrderRequestBody>();
        if (body is null || string.IsNullOrWhiteSpace(body.ReasonCode))
        {
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "RESTAURANT_INVALID_REASON",
                "Request body must include a reasonCode.");
        }

        try
        {
            // --- Dispatch the command via MediatR ---
            var command = new RejectOrderCommand
            {
                OrderId = parsedOrderId,
                RestaurantId = parsedRestaurantId,
                ReasonCode = body.ReasonCode,
                Notes = body.Notes
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
            _logger.LogWarning(ex, "Order state invalid for reject: {OrderId}, current status: {Status}",
                parsedOrderId, ex.CurrentStatus);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, ex.ErrorCode, ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid reason code for order: {OrderId}", parsedOrderId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "RESTAURANT_INVALID_REASON",
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error rejecting order: {OrderId}", parsedOrderId);
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
/// Request body for the RejectOrder endpoint.
/// </summary>
internal sealed class RejectOrderRequestBody
{
    public string ReasonCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
