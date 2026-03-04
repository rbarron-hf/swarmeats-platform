using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Orders.Domain.Commands;
using Orders.Domain.Exceptions;
using Orders.Functions.Models;

namespace Orders.Functions;

/// <summary>
/// Azure Function HTTP endpoint for cancelling an order.
/// POST /orders/{orderId}/cancel
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
/// </summary>
public sealed class CancelOrderFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<CancelOrderFunction> _logger;

    public CancelOrderFunction(IMediator mediator, ILogger<CancelOrderFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("CancelOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders/{orderId}/cancel")]
        HttpRequestData request,
        string orderId)
    {
        _logger.LogInformation("Cancel order request received for orderId: {OrderId}", orderId);

        // --- Parse and validate the route parameter ---
        if (!Guid.TryParse(orderId, out var parsedOrderId))
        {
            _logger.LogWarning("Invalid orderId format: {OrderId}", orderId);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_ORDER_ID",
                "The orderId must be a valid GUID.");
        }

        try
        {
            // --- Dispatch the command via MediatR ---
            var command = new CancelOrderCommand { OrderId = parsedOrderId };
            var result = await _mediator.Send(command);

            // --- Return 200 with the updated order ---
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (OrderNotFoundException ex)
        {
            _logger.LogWarning(ex, "Order not found: {OrderId}", parsedOrderId);
            return await CreateErrorResponse(request, HttpStatusCode.NotFound, ex.ErrorCode, ex.Message);
        }
        catch (OrderCannotBeCancelledException ex)
        {
            _logger.LogWarning(ex, "Order cannot be cancelled: {OrderId}, current status: {Status}",
                parsedOrderId, ex.CurrentStatus);
            return await CreateErrorResponse(request, HttpStatusCode.Conflict, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error cancelling order: {OrderId}", parsedOrderId);
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
