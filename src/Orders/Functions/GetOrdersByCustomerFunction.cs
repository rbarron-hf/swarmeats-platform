using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Orders.Domain.Queries;
using Orders.Functions.Models;

namespace Orders.Functions;

/// <summary>
/// Azure Function HTTP endpoint for retrieving all orders for a customer.
/// GET /orders?customerId={customerId}&amp;continuationToken={continuationToken}
/// Isolated worker model. Delegates to MediatR query pipeline.
/// </summary>
public sealed class GetOrdersByCustomerFunction
{
    private readonly IMediator _mediator;
    private readonly ILogger<GetOrdersByCustomerFunction> _logger;

    public GetOrdersByCustomerFunction(IMediator mediator, ILogger<GetOrdersByCustomerFunction> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetOrdersByCustomer")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders")]
        HttpRequestData request)
    {
        _logger.LogInformation("Get orders by customer request received.");

        // --- Parse and validate query parameters ---
        var queryParams = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var customerIdParam = queryParams["customerId"];

        if (string.IsNullOrWhiteSpace(customerIdParam) || !Guid.TryParse(customerIdParam, out var customerId) || customerId == Guid.Empty)
        {
            _logger.LogWarning("Invalid or missing customerId query parameter: {CustomerId}", customerIdParam);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest, "ORDER_INVALID_CUSTOMER",
                "The customerId query parameter must be a valid, non-empty GUID.");
        }

        var continuationToken = queryParams["continuationToken"];

        try
        {
            // --- Dispatch the query via MediatR ---
            var query = new GetOrdersByCustomerQuery
            {
                CustomerId = customerId,
                ContinuationToken = continuationToken
            };
            var result = await _mediator.Send(query);

            // --- Return 200 with the paginated order summaries ---
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving orders for customer: {CustomerId}", customerId);
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
