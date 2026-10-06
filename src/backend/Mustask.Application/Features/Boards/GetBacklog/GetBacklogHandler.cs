using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// A team's backlog of one level (US-1): the items § Model's conditions 1–4
/// select at this moment (BR-M4-5), placed ones by their places, then the
/// unplaced ones by number (BR-M4-24), numbered «Порядок» 1..n. With feat-001's
/// tree (its api-contract § 4, BR-H-9..-13): each row's live parent, and beneath
/// it the same team's backlog one level below, read the same way, nested by the
/// links in that backlog's own order — «Эпики» three tiers, «Фичи» two,
/// «Истории» one.
/// </summary>
/// <remarks>
/// Not found from the outside in (api-contract § 0.3): the organization (the
/// filter has answered), the project — malformed, unknown, of another
/// organization or not reachable — the team — malformed, unknown, of another
/// project or deleted (BR-M4-20) — then the level, by the organization's
/// process. Reads only, no transaction: each level is its own snapshot, so a
/// link written between two of them shows at the next read (BR-H-19).
/// </remarks>
public sealed class GetBacklogHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IBacklogReadModel backlogs)
{
    public async Task<BacklogResponse> Handle(GetBacklogQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(projectId, cancellationToken) is not { IsReachable: true } project
            || project.OrganizationId != organizationId)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await structure.FindTeamAsync(teamId, cancellationToken) is not { IsReachable: true } team
            || team.ProjectId != projectId)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        if (!BoardLevelNames.TryParse(query.Level, out var level)
            || await processes.FindByOrganizationAsync(organizationId, cancellationToken) is not { } process
            || level.Resolve(process.BacklogLevels.Select(row => new ProcessLevel(row.Id, row.Rank, row.Kind))) is not { } resolved)
        {
            throw new NotFoundException(BoardErrorCodes.LevelNotFound);
        }

        var processLevel = process.BacklogLevels.Single(row => row.Id == resolved.Id);
        var processLevels = process.BacklogLevels.Select(row => new ProcessLevel(row.Id, row.Rank, row.Kind)).ToList();
        var types = process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id).ToList();

        var read = await backlogs.ReadAsync(BacklogScope.Of(projectId, teamId, resolved.Id, types), cancellationToken);

        // feat-001: the same team's backlogs below, one level at a time, while the process has the level.
        var levels = new List<IReadOnlyList<BacklogItemRow>> { read.Items };

        for (var below = level.Below(); below?.Resolve(processLevels) is { } lower; below = below.Value.Below())
        {
            var lowerTypes = process.WorkItemTypes.Where(type => type.BacklogLevelId == lower.Id);
            levels.Add((await backlogs.ReadAsync(BacklogScope.Of(projectId, teamId, lower.Id, lowerTypes), cancellationToken)).Items);
        }

        var parentIds = levels.SelectMany(rows => rows).Select(row => row.ParentId).OfType<Guid>().ToList();
        var liveParents = await backlogs.FindLiveParentsAsync(projectId, parentIds, cancellationToken);

        return new BacklogResponse(
            new BoardTeamView(team.Id, team.Name, read.DefaultArea),
            BoardLevelView.Of(level, processLevel, types),
            BacklogRowView.Tree(levels, liveParents));
    }
}
