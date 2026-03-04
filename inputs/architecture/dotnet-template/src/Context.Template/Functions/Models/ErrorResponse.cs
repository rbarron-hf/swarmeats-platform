namespace {ContextName}.Functions.Models;

/// <summary>
/// Standard error response DTO for all API error payloads.
/// Used by every HTTP function in this bounded context.
///
/// JSON format:
/// {
///     "errorCode": "ORDER_NOT_FOUND",
///     "message": "Order with ID '...' was not found."
/// }
/// </summary>
internal sealed class ErrorResponse
{
    /// <summary>
    /// Machine-readable error code for API consumers.
    /// Naming convention: {ENTITY}_{ERROR_TYPE} (e.g., ORDER_NOT_FOUND).
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message. May include entity identifiers
    /// but must never include stack traces or internal details.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
