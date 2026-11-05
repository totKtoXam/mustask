using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// A team's backlog page of one level (mod-006 US-1, feat-040 US-1, US-2) with its tree at full depth (feat-027 US-1, US-2;
/// api-contract § 1, § 2): the tree set — the live items the team owns (mod-017 BR-OT-5; an item's area decides nothing) in
/// every active project of the organization, of this level and of every level ranked below it, the «Задачи» level
/// included — read at this moment; the top-level rows in the team's order (BR-TS-6), numbered «Порядок» 1..n; beneath each
/// its children, by level then each level's team order, recursively; every row's «Проект» and «Родитель». The project filter
/// (BR-TS-8) narrows the tree set in the same statement.
/// </summary>
/// <remarks>
/// Not found from the outside in (api-contract § 0.3): the organization (the scoped check has answered), the team — malformed,
/// unknown, of another organization or deleted (BR-M4-20, BR-TS-13) — then the level, by the organization's process. There is
/// no project in the address, so no project can be missing. Reads only, no transaction, a fixed number of statements: the
/// team, the process, the team's linked projects, the tree set at once whatever the depth, the live parents outside it, and the
/// team again — a team deleted while the reads ran answers its not-found rather than an empty view (tech-011 m2, plan-backend
/// PD-10). The caller (organization, user, role) is a required argument of the read (BR-TS-13, ADR-0029 § 7).
/// </remarks>
public sealed class GetBacklogHandler(
    IOrganizationStructure structure,
    IProcessDefinitionReadModel processes,
    IBacklogReadModel backlogs,
    IOrganizationAccess access,
    ICurrentUser currentUser)
{
    public async Task<BacklogResponse> Handle(GetBacklogQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await structure.FindTeamAsync(organizationId, teamId, cancellationToken) is not { IsReachable: true } team)
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

        // The team's linked active projects: the filter's options, the empty state and the create command's list. Never narrowed
        // by the filter below (api-contract § 2.2).
        var linked = await structure.ListLinkedProjectsAsync(organizationId, teamId, cancellationToken);

        // The project filter (BR-TS-8) narrows the tree set on the same statement: the tree is built over the matching items only,
        // and a parent outside it is read as any parent outside the set (its «Родитель» cell, unfiltered).
        var filter = WorkItemFilter.OfProjects(query.ProjectIds);
        var caller = new CallerAccess(organizationId, currentUser.SubjectId, access.Role);

        var rows = await backlogs.ReadTeamTreeAsync(TeamBacklogTreeScope.Of(teamId, treeTypes, filter), caller, cancellationToken);

        // A parent inside the set is live and is its own row; only those outside it are read (feat-001 BR-H-13).
        var inSet = rows.Select(row => row.Id).ToHashSet();
        var parentsOutside = rows.Select(row => row.ParentId).OfType<Guid>().Where(id => !inSet.Contains(id)).Distinct().ToList();
        var liveParentsOutside = await backlogs.FindLiveParentsAsync(organizationId, parentsOutside, cancellationToken);

        // tech-011 m2: a team deleted while the reads ran is not an empty view but its not-found; read once more, last.
        if (await structure.FindTeamAsync(organizationId, teamId, cancellationToken) is not { IsReachable: true })
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        return new BacklogResponse(
            new BoardTeamView(team.Id, team.Name),
            linked.Select(project => new BoardProjectView(project.Id, project.Name)).ToList(),
            BoardLevelView.Of(level, processLevel, types),
            BacklogRowView.Tree(resolved.Rank, rows, liveParentsOutside));
    }
}
