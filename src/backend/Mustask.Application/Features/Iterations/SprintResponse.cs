using Mustask.Domain.Iterations;

namespace Mustask.Application.Features.Iterations;

/// <summary>A sprint on the wire (api-contract § 1): the state is its name as text.</summary>
public sealed record SprintResponse(
    Guid Id,
    Guid TeamId,
    int Number,
    string Name,
    string State,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ClosedAt,
    string? ClosedBy)
{
    public static SprintResponse From(Sprint sprint) =>
        new(
            sprint.Id,
            sprint.TeamId,
            sprint.Number,
            sprint.Name,
            sprint.State.ToString(),
            sprint.StartDate,
            sprint.EndDate,
            sprint.CreatedAt,
            sprint.CreatedBy,
            sprint.ClosedAt,
            sprint.ClosedBy);
}
