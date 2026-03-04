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
/// Azure Function HTTP endpoint for placing a new order.
/// POST /orders
/// Isolated worker model. Validates request body and delegates business logic to MediatR pipeline.
/// </summary>
public sealed class PlaceOrderFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<PlaceOrderFunction> _logger;

    public PlaceOrderFunction(IMediator mediator, ILogger<PlaceOrderFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("PlaceOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")]
        HttpRequestData request)
    {
        _logger.LogInformation("Place order request received.");

        try
        {
            // --- Deserialize the request body ---
            var command = await request.ReadFromJsonAsync<PlaceOrderCommand>();

            if (command is null)
            {
                _logger.LogWarning("Request body is null or could not be deserialized.");
                return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "INVALID_REQUEST",
                    "Request body is required and must be valid JSON.");
            }

            // --- Dispatch the command via MediatR ---
            var result = await _mediator.Send(command);

            // --- Return 201 Created with the order details ---
            var response = request.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (InvalidOrderException ex)
        {
            _logger.LogWarning(ex, "Order validation failed: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error placing order.");
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
