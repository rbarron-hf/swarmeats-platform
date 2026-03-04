using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Restaurant.Domain.Exceptions;
using Restaurant.Domain.Queries;
using Restaurant.Functions.Models;

namespace Restaurant.Functions;

/// <summary>
/// Azure Function HTTP endpoint for retrieving a restaurant's menu.
/// GET /restaurants/{restaurantId}/menu
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// RST-001.
/// </summary>
public sealed class GetMenuFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetMenuFunction> _logger;

    public GetMenuFunction(IMediator mediator, ILogger<GetMenuFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetMenu")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "restaurants/{restaurantId}/menu")]
        HttpRequestData request,
        string restaurantId)
    {
        _logger.LogInformation("Get menu request received for restaurantId: {RestaurantId}", restaurantId);

        // --- Parse and validate the route parameter ---
        if (!Guid.TryParse(restaurantId, out var parsedRestaurantId))
        {
            _logger.LogWarning("Invalid restaurantId format: {RestaurantId}", restaurantId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_RESTAURANT_ID",
                "The restaurantId must be a valid GUID.");
        }

        try
        {
            // --- Dispatch the query via MediatR ---
            var query = new GetMenuQuery { RestaurantId = parsedRestaurantId };
            var result = await _mediator.Send(query);

            // --- Return 200 with the menu ---
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (MenuNotFoundException ex)
        {
            _logger.LogWarning(ex, "Menu not found for restaurant: {RestaurantId}", parsedRestaurantId);
            return await CreateErrorResponse(request, HttpStatusCode.NotFound, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving menu for restaurant: {RestaurantId}", parsedRestaurantId);
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
