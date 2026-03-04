namespace {ContextName}.Domain.Exceptions;

/// <summary>
/// Thrown when {description of the error condition}.
/// Maps to HTTP {statusCode} with error code {ERROR_CODE}.
/// </summary>
public sealed class {ExceptionName} : Exception
{
    /// <summary>
    /// Identifier of the affected aggregate.
    /// </summary>
    public Guid {AggregateId}Id { get; }

    /// <summary>
    /// Machine-readable error code for API consumers.
    /// </summary>
    public string ErrorCode => "{ERROR_CODE}";

    public {ExceptionName}(Guid aggregateId)
        : base($"{Aggregate} with ID '{aggregateId}' {error description}.")
    {
        {AggregateId}Id = aggregateId;
    }

    public {ExceptionName}(Guid aggregateId, Exception innerException)
        : base($"{Aggregate} with ID '{aggregateId}' {error description}.", innerException)
    {
        {AggregateId}Id = aggregateId;
    }
}
