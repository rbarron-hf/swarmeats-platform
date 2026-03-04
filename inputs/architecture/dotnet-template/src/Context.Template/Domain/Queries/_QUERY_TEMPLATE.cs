using MediatR;

namespace {ContextName}.Domain.Queries;

/// <summary>
/// Query to retrieve {description}.
/// This is a read-only operation — no state changes or domain events.
/// </summary>
public sealed record {QueryName} : IRequest<{ResponseName}>
{
    /// <summary>
    /// Identifier of the aggregate to retrieve.
    /// </summary>
    public required Guid {AggregateId}Id { get; init; }
}

/// <summary>
/// Response DTO containing the full {aggregate} details.
/// </summary>
public sealed record {ResponseName}
{
    // TODO: Add all response fields matching the AC node data contract.
    // Use nested records for complex nested objects (line items, addresses, etc.).
    // Example:
    //   public required Guid Id { get; init; }
    //   public required string Status { get; init; }
    //   public required List<{Nested}Response> Items { get; init; }
}
