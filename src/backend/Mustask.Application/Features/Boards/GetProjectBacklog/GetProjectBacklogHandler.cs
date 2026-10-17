using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetProjectBacklog;

/// <summary>
/// A project's backlog page of one level (feat-039 US-1; api-contract § 1, § 3.1): the tree set — the project's live
/// items of this level and of every level ranked below it, the «Задачи» level included, whatever their area or team —
/// read at this moment; the top-level rows in the project's order (BR-PB-4), numbered «Порядок» 1..n; beneath each
/// its children, by level then each level's project order, recursively; every row's «Родитель».
/// </summary>
/// <remarks>
/// Not found from the outside in (api-contract § 0.3): the organization (the scoped check has answered), the project —
/// malformed, unknown, of another organization or not reachable — then the level, by the organization's process. A
/// project page has no team, so no team is looked up and none can be missing. Reads only, no transaction, a fixed
/// number of statements: the tree set at once, whatever the depth, then the live parents outside it.
/// </remarks>
public sealed class GetProjectBacklogHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IBacklogReadModel backlogs)
{
    public async Task<ProjectBacklogResponse> Handle(GetProjectBacklogQuery query, CancellationToken cancellationToken)
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

        if (!BoardLevelNames.TryParse(query.Level, out var level)
            || await processes.FindByOrganizationAsync(organizationId, cancellationToken) is not { } process
            || level.Resolve(process.BacklogLevels.Select(row => new ProcessLevel(row.Id, row.Rank, row.Kind))) is not { } resolved)
        {
            throw new NotFoundException(BoardErrorCodes.LevelNotFound);
        }

        var processLevel = process.BacklogLevels.Single(row => row.Id == resolved.Id);
        var types = process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id).ToList();

        // The tree set: the project's backlog of this level and those of every level ranked below it (api-contract § 1).
        var treeLevels = process.BacklogLevels.Where(row => row.Rank <= resolved.Rank).Select(row => row.Id).ToHashSet();
        var treeTypes = process.WorkItemTypes.Where(type => treeLevels.Contains(type.BacklogLevelId));

        // The team filter (mod-017 BR-OT-8) narrows the tree set on the same statement: the tree is built over the matching
        // items only, and a parent outside it is read as any parent outside the set (its «Родитель» cell, unfiltered).
        var filter = WorkItemFilter.OfTeams(query.TeamIds);

        var rows = await backlogs.ReadProjectTreeAsync(ProjectBacklogTreeScope.Of(projectId, treeTypes, filter), cancellationToken);

        // A parent inside the set is live and is its own row; only those outside it are read (feat-001 BR-H-13).
        var inSet = rows.Select(row => row.Id).ToHashSet();
        var parentsOutside = rows.Select(row => row.ParentId).OfType<Guid>().Where(id => !inSet.Contains(id)).Distinct().ToList();
        var liveParentsOutside = await backlogs.FindLiveParentsAsync(projectId, parentsOutside, cancellationToken);

        return new ProjectBacklogResponse(
            new BoardProjectView(project.Id, project.Name),
            BoardLevelView.Of(level, processLevel, types),
            BacklogRowView.Tree(resolved.Rank, rows, liveParentsOutside));
    }
}
