using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>An item of a backlog and its place on that backlog, <c>null</c> when unplaced there.</summary>
internal sealed class BacklogMember
{
    public required WorkItem Item { get; init; }

    public required int? Place { get; init; }
}

/// <summary>
/// The one definition of what a team's backlog and board hold (mod-017 BR-OT-5, replacing mod-006 BR-M4-5; feat-040 api-contract
/// § 1, BR-TS-2, across the team's projects), used by the backlog read, the board read and the reorder alike:
/// <list type="number">
/// <item>the team <b>owns</b> the item — its owning team is the team; its project, area, the area's owner and the area's moves
/// decide nothing, and no area tree is read. A team owns items only in the projects it is linked to (feat-028 BR-OL-6, held by
/// the owner key), so no link is read;</item>
/// <item>the item's project is an <b>active project of the caller's organization</b> — the tenant predicate (BR-TS-13) and the
/// reachability of the project in one join, in the same snapshot as the items: a deactivated project's items leave the view and
/// come back with their places when it is restored (ADR-0028 rule 9);</item>
/// <item>its type is one of the level's;</item>
/// <item>its state is outside the Removed category — the one definition of "deleted" (mod-005, ADR-0030 rule 2), taken from
/// the scope's shown-state ids; <c>DeletedStateParityTests</c> pins it equal to <c>WorkItemSets.Live</c>;</item>
/// <item>(a view only) its project is any of the filter's projects (BR-TS-8), through <see cref="WorkItemFilterPredicate"/>
/// on the same statement: it adds none.</item>
/// </list>
/// All of it goes into one <c>work_items</c> statement, served by <c>ix_work_items_owning_team_id_type_id</c>, which joins the
/// project by its key and left-joins the item's place on this backlog by the place's key. The reorder's locking read
/// (<see cref="BacklogRepository"/>) states the same conditions without the filter in SQL; a test pins that the two agree.
/// </summary>
internal static class BacklogMembership
{
    /// <summary>
    /// The backlog's members with their places on it, composed on
    /// <see cref="IQueryable"/> so it runs in SQL. The place is joined by its key
    /// alone, the item, and kept only when it is on this backlog: a join on a
    /// unique key is planned as one lookup per item whatever the statistics say,
    /// whereas filtering the places by team and level first lets a planner with
    /// stale statistics (a backlog placed a moment ago) expect one row and scan
    /// all of them for every item.
    /// </summary>
    public static IQueryable<BacklogMember> Members(MustaskDbContext context, TeamBacklogScope scope, Guid organizationId)
    {
        var typeIds = scope.TypeIds.ToList();
        var stateIds = scope.ShownStateIds.ToList();
        var teamId = scope.TeamId;
        var levelId = scope.LevelId;

        return from item in WorkItemFilterPredicate.Apply(context, context.WorkItems.AsNoTracking(), scope.Filter)
               where item.OwningTeamId == teamId
                   && typeIds.Contains(item.TypeId)
                   && stateIds.Contains(item.StateId)
               join project in context.Projects.AsNoTracking() on item.ProjectId equals project.Id
               where project.OrganizationId == organizationId && project.Status == ProjectStatus.Active
               join place in context.Set<BacklogPlace>().AsNoTracking() on item.Id equals place.WorkItemId into placed
               from place in placed.DefaultIfEmpty()
               select new BacklogMember
               {
                   Item = item,
                   Place = place != null && place.TeamId == teamId && place.BacklogLevelId == levelId ? place.Position : null,
               };
    }

    /// <summary>
    /// feat-027 (api-contract § 1): the members of a page's tree set — the same conditions over every level of
    /// <paramref name="scope"/> at once, in one statement — each with its place on the team's backlog of its own type's
    /// level, joined by the place's key as <see cref="Members"/> joins it.
    /// </summary>
    public static IQueryable<BacklogMember> TreeMembers(MustaskDbContext context, TeamBacklogTreeScope scope, Guid organizationId)
    {
        var typeIds = scope.TypeIds.ToList();
        var stateIds = scope.ShownStateIds.ToList();
        var teamId = scope.TeamId;

        return from item in WorkItemFilterPredicate.Apply(context, context.WorkItems.AsNoTracking(), scope.Filter)
               where item.OwningTeamId == teamId
                   && typeIds.Contains(item.TypeId)
                   && stateIds.Contains(item.StateId)
               join project in context.Projects.AsNoTracking() on item.ProjectId equals project.Id
               where project.OrganizationId == organizationId && project.Status == ProjectStatus.Active
               join type in context.WorkItemTypes.AsNoTracking() on item.TypeId equals type.Id
               join place in context.Set<BacklogPlace>().AsNoTracking() on item.Id equals place.WorkItemId into placed
               from place in placed.DefaultIfEmpty()
               select new BacklogMember
               {
                   Item = item,
                   Place = place != null && place.TeamId == teamId && place.BacklogLevelId == type.BacklogLevelId ? place.Position : null,
               };
    }
}
