using Mustask.Application.Abstractions.Iterations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Iterations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Iterations;

/// <summary>
/// What the page reads of a Closed sprint share (US-9, US-10; api-contract § 2.9): the team and the sprint of the address (a
/// sprint that is not Closed is not found), the level of the process, the range and the team's other Closed sprints in it.
/// </summary>
internal sealed record OutcomeScope(
    Guid TeamId,
    SprintView Sprint,
    string LevelName,
    Guid LevelId,
    SprintRange Range,
    RangeSprints RangeSprints)
{
    public static async Task<OutcomeScope> ResolveAsync(
        IOrganizationStructure structure,
        IProcessDefinitionReadModel processes,
        ISprintReadModel sprints,
        string? organizationId,
        string? teamId,
        string? sprintId,
        string? level,
        string? from,
        string? to,
        CancellationToken cancellationToken)
    {
        var organization = SprintAddress.Organization(organizationId);
        var team = await SprintAddress.TeamAsync(structure, organization, teamId, cancellationToken);
        var id = SprintAddress.Sprint(sprintId);

        var sprint = await sprints.FindAsync(team, id, cancellationToken);

        if (sprint is not { State: nameof(SprintState.Closed) })
        {
            throw SprintAddress.SprintNotFound();
        }

        var (name, levelId) = await LevelAsync(processes, organization, level ?? BoardLevel.Stories.ToWireName(), cancellationToken);
        var range = SprintRange.Resolve(from, to, sprint.StartDate ?? sprint.EndDate ?? DateOnly.FromDateTime(DateTime.UtcNow));

        return new OutcomeScope(team, sprint, name, levelId, range, await sprints.FindRangeAsync(team, id, range, cancellationToken));
    }

    /// <summary>The level of the address in the organization's process; none is 404 <c>boards.level.not_found</c>.</summary>
    public static async Task<(string Name, Guid Id)> LevelAsync(
        IProcessDefinitionReadModel processes, Guid organizationId, string level, CancellationToken cancellationToken)
    {
        if (!BoardLevelNames.TryParse(level, out var parsed)
            || await processes.FindByOrganizationAsync(organizationId, cancellationToken) is not { } process
            || parsed.Resolve(process.BacklogLevels.Select(row => new ProcessLevel(row.Id, row.Rank, row.Kind))) is not { } resolved)
        {
            throw new NotFoundException(BoardErrorCodes.LevelNotFound);
        }

        return (parsed.ToWireName(), resolved.Id);
    }
}
