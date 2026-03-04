using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Orders.Domain.Exceptions;
using Orders.Domain.Queries;
using Orders.Functions.Models;

namespace Orders.Functions;

/// <summary>
/// Azure Function HTTP endpoint for retrieving a single order by ID.
/// GET /orders/{orderId}
/// Isolated worker model. Delegates to MediatR query pipeline.
/// </summary>
public sealed class GetOrderFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetOrderFunction> _logger;

    public GetOrderFunction(IMediator mediator, ILogger<GetOrderFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/{orderId}")]
        HttpRequestData request,
        string orderId)
    {
        _logger.LogInformation("Get order request received for orderId: {OrderId}", orderId);

        // --- Parse and validate the route parameter ---
        if (!Guid.TryParse(orderId, out var parsedOrderId))
        {
            _logger.LogWarning("Invalid orderId format: {OrderId}", orderId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_ORDER_ID",
                "The orderId must be a valid GUID.");
        }

        try
        {
            // --- Dispatch the query via MediatR ---
            var query = new GetOrderQuery { OrderId = parsedOrderId };
            var result = await _mediator.Send(query);

            // --- Return 200 with the full order ---
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (OrderNotFoundException ex)
        {
            _logger.LogWarning(ex, "Order not found: {OrderId}", parsedOrderId);
            return await CreateErrorResponse(request, HttpStatusCode.NotFound, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving order: {OrderId}", parsedOrderId);
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
