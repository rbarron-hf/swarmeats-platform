using MediatR;
using {ContextName}.Domain.Exceptions;
using {ContextName}.Infrastructure.Repositories;

namespace {ContextName}.Domain.Queries;

/// <summary>
/// Handles the <see cref="{QueryName}"/> by loading the aggregate
/// from the repository and mapping it to a response DTO.
/// Pure read operation — no business logic, no state changes, no events.
/// </summary>
public sealed class {QueryHandlerName} : IRequestHandler<{QueryName}, {ResponseName}>
{
    private readonly I{Aggregate}Repository _repository;

    public {QueryHandlerName}(I{Aggregate}Repository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<{ResponseName}> Handle({QueryName} request, CancellationToken cancellationToken)
    {
        var aggregate = await _repository.GetByIdAsync(request.{AggregateId}Id, cancellationToken);

        if (aggregate is null)
        {
            throw new {Aggregate}NotFoundException(request.{AggregateId}Id);
        }

        return MapToResponse(aggregate);
    }

    private static {ResponseName} MapToResponse({AggregateType} aggregate)
    {
        return new {ResponseName}
        {
            // TODO: Map all aggregate fields to the response DTO.
        };
    }
}
