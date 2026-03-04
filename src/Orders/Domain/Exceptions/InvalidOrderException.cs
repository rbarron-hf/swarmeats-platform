namespace Orders.Domain.Exceptions;

/// <summary>
/// Thrown when order placement validation fails due to business rule violations
/// (e.g., minimum order value not met, too many items, invalid address).
/// Maps to HTTP 400 with the specific error code describing the validation failure.
/// </summary>
public sealed class InvalidOrderException : Exception
{
    /// <summary>
    /// Machine-readable error code identifying the specific validation failure.
    /// </summary>
    public string ErrorCode { get; }

    public InvalidOrderException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public InvalidOrderException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
