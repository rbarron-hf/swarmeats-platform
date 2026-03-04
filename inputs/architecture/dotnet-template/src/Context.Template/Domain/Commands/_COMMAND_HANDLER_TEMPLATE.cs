using MediatR;
using {ContextName}.Domain.Exceptions;
using {ContextName}.Infrastructure.Repositories;

namespace {ContextName}.Domain.Commands;

/// <summary>
/// Handles the <see cref="{CommandName}"/> by loading the aggregate,
/// delegating to the aggregate's method, and persisting the result.
///
/// IMPORTANT: No business logic lives here. All rules are enforced
/// inside the aggregate. This handler only orchestrates:
///   1. Load aggregate from repository
///   2. Call aggregate method (which enforces rules + raises events)
///   3. Save aggregate (repository handles outbox)
///   4. Return result DTO
/// </summary>
public sealed class {HandlerName} : IRequestHandler<{CommandName}, {ResultName}>
{
    private readonly I{Aggregate}Repository _repository;

    public {HandlerName}(I{Aggregate}Repository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<{ResultName}> Handle({CommandName} request, CancellationToken cancellationToken)
    {
        // 1. Load
        var aggregate = await _repository.GetByIdAsync(request.{AggregateId}Id, cancellationToken);

        if (aggregate is null)
        {
            throw new {Aggregate}NotFoundException(request.{AggregateId}Id);
        }

        // 2. Delegate — all business rules enforced inside the aggregate
        aggregate.{Method}(/* pass command parameters */);

        // 3. Save — repository handles outbox pattern for domain events
        await _repository.SaveAsync(aggregate, cancellationToken);

        // 4. Return result
        return new {ResultName}
        {
            {AggregateId}Id = aggregate.Id,
            Status = aggregate.Status.ToString()
            // TODO: Map additional fields
        };
    }
}
