using MediatR;

namespace {ContextName}.Domain.Commands;

/// <summary>
/// Command to {description of what this command does}.
/// Dispatched from {FunctionName} to {HandlerName} via MediatR.
/// </summary>
public sealed record {CommandName} : IRequest<{ResultName}>
{
    /// <summary>
    /// Identifier of the target aggregate.
    /// </summary>
    public required Guid {AggregateId}Id { get; init; }

    // TODO: Add command-specific parameters (e.g., estimatedPrepTime, rejectionReason).
}

/// <summary>
/// Result returned after successful execution of <see cref="{CommandName}"/>.
/// </summary>
public sealed record {ResultName}
{
    public required Guid {AggregateId}Id { get; init; }
    public required string Status { get; init; }

    // TODO: Add result-specific fields matching the AC node response schema.
}
