using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// A team's backlog page of one level (mod-006 US-1) with its tree at full depth
/// (feat-027 US-1, US-2; api-contract § 1, § 2): the tree set — the team's live
/// items of this level and of every level ranked below it, the «Задачи» level
/// included — read at this moment; the top-level rows in the backlog's order
/// (BR-M4-24), numbered «Порядок» 1..n; beneath each its children, by level then
/// each level's backlog order, recursively; every row's «Родитель».
/// </summary>
/// <remarks>
/// Not found from the outside in (mod-006 api-contract § 0.3): the organization
/// (the filter has answered), the project — malformed, unknown, of another
/// organization or not reachable — the team — malformed, unknown, of another
/// project or deleted (BR-M4-20) — then the level, by the organization's
/// process. Reads only, no transaction, a fixed number of statements: the tree
/// set at once, whatever the depth, then the live parents outside it. A link
/// written between the two shows at the next read (BR-T-15).
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
        var types = process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id).ToList();

        // The tree set: this level's backlog and those of every level ranked below it (api-contract § 1).
        var treeLevels = process.BacklogLevels.Where(row => row.Rank <= resolved.Rank).Select(row => row.Id).ToHashSet();
        var treeTypes = process.WorkItemTypes.Where(type => treeLevels.Contains(type.BacklogLevelId));

        var read = await backlogs.ReadTreeAsync(BacklogTreeScope.Of(projectId, teamId, treeTypes), cancellationToken);

        // A parent inside the set is live and is its own row; only those outside it are read (feat-001 BR-H-13).
        var inSet = read.Items.Select(row => row.Id).ToHashSet();
        var parentsOutside = read.Items.Select(row => row.ParentId).OfType<Guid>().Where(id => !inSet.Contains(id)).Distinct().ToList();
        var liveParentsOutside = await backlogs.FindLiveParentsAsync(projectId, parentsOutside, cancellationToken);

        return new BacklogResponse(
            new BoardTeamView(team.Id, team.Name, read.DefaultArea),
            BoardLevelView.Of(level, processLevel, types),
            BacklogRowView.Tree(resolved.Rank, read.Items, liveParentsOutside));
    }
}
