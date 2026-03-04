using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Restaurant.Domain.Queries;
using Restaurant.Domain.ValueObjects;
using Restaurant.Functions.Models;

namespace Restaurant.Functions;

/// <summary>
/// Azure Function HTTP endpoint for retrieving a restaurant's active orders.
/// GET /restaurants/{restaurantId}/orders?status={status}
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// RST-002.
/// </summary>
public sealed class GetActiveOrdersFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetActiveOrdersFunction> _logger;

    public GetActiveOrdersFunction(IMediator mediator, ILogger<GetActiveOrdersFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetActiveOrders")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "restaurants/{restaurantId}/orders")]
        HttpRequestData request,
        string restaurantId)
    {
        _logger.LogInformation("Get active orders request received for restaurantId: {RestaurantId}", restaurantId);

        // --- Parse and validate the route parameter ---
        if (!Guid.TryParse(restaurantId, out var parsedRestaurantId))
        {
            _logger.LogWarning("Invalid restaurantId format: {RestaurantId}", restaurantId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_RESTAURANT_ID",
                "The restaurantId must be a valid GUID.");
        }

        // --- Parse optional status query parameter ---
        RestaurantOrderStatus? statusFilter = null;
        var queryParams = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var statusParam = queryParams["status"];

        if (!string.IsNullOrEmpty(statusParam))
        {
            if (!Enum.TryParse<RestaurantOrderStatus>(statusParam, ignoreCase: true, out var parsedStatus))
            {
                _logger.LogWarning("Invalid status parameter: {Status}", statusParam);
                return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "RESTAURANT_INVALID_STATUS",
                    $"The status '{statusParam}' is not valid. Allowed values: Pending, Accepted, Preparing, ReadyForPickup, Rejected.");
            }
            statusFilter = parsedStatus;
        }

        try
        {
            // --- Dispatch the query via MediatR ---
            var query = new GetActiveOrdersQuery
            {
                RestaurantId = parsedRestaurantId,
                Status = statusFilter
            };
            var result = await _mediator.Send(query);

            // --- Return 200 with the orders ---
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving orders for restaurant: {RestaurantId}", parsedRestaurantId);
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
