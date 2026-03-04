using System.Net;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using {ContextName}.Domain.Commands;  // or Queries
using {ContextName}.Domain.Exceptions;
using {ContextName}.Functions.Models;

namespace {ContextName}.Functions;

/// <summary>
/// Azure Function HTTP endpoint for {description}.
/// {METHOD} /{route}
/// Isolated worker model. Delegates all business logic to MediatR pipeline.
///
/// Story: {STORY_ID}
/// AC Node: {AC_NODE_ID}
/// </summary>
public sealed class {FunctionName}
{
    private readonly IMediator _mediator;
    private readonly ILogger<{FunctionName}> _logger;

    public {FunctionName}(IMediator mediator, ILogger<{FunctionName}> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("{FunctionName}")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "{method}", Route = "{route}")]
        HttpRequestData request,
        string routeParam)
    {
        _logger.LogInformation("{FunctionName} request received for: {Param}", routeParam);

        // ── 1. Parse and validate route parameters ──
        if (!Guid.TryParse(routeParam, out var parsedId))
        {
            _logger.LogWarning("Invalid parameter format: {Param}", routeParam);
            return await CreateErrorResponse(request, HttpStatusCode.BadRequest,
                "INVALID_ID", "The parameter must be a valid GUID.");
        }

        try
        {
            // ── 2. Dispatch command/query via MediatR ──
            var command = new {CommandOrQuery} { Id = parsedId };
            var result = await _mediator.Send(command);

            // ── 3. Return success response ──
            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch ({Aggregate}NotFoundException ex)
        {
            _logger.LogWarning(ex, "Not found: {Id}", parsedId);
            return await CreateErrorResponse(request, HttpStatusCode.NotFound,
                ex.ErrorCode, ex.Message);
        }
        // TODO: Add catch blocks for context-specific exceptions:
        //   catch (InvalidOrderException ex) => 400
        //   catch (InvalidOrderStateException ex) => 409
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error: {Id}", parsedId);
            return await CreateErrorResponse(request, HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred.");
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
