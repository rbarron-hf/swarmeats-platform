namespace Delivery.Functions.Models;

/// <summary>
/// Standard error response DTO for API error payloads.
/// </summary>
internal sealed class ErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
