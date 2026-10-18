using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>An item of a backlog and its place on that backlog, <c>null</c> when unplaced there.</summary>
internal sealed class BacklogMember
{
    public required WorkItem Item { get; init; }

    public required int? Place { get; init; }
}

/// <summary>
/// The one definition of what a team's backlog and board hold (mod-017 BR-OT-5, replacing mod-006 BR-M4-5), used by the
/// backlog read, the board read and the reorder alike:
/// <list type="number">
/// <item>the item belongs to the project (the caller has checked the team is an active team of it);</item>
/// <item>its type is one of the level's;</item>
/// <item>the team <b>owns</b> it — its owning team is the team; its area, the area's owner and the area's moves decide
/// nothing, and no area tree is read;</item>
/// <item>its state is outside the Removed category — the one definition of "deleted" (mod-005, ADR-0030 rule 2), taken from
/// the scope's shown-state ids; <c>DeletedStateParityTests</c> pins it equal to <c>WorkItemSets.Live</c>.</item>
/// </list>
/// All four go into one <c>work_items</c> statement, served by <c>ix_work_items_project_id_owning_team_id_type_id</c>, which
/// left-joins the item's place on this backlog by the place's key. The reorder's locking read
/// (<see cref="BacklogRepository"/>) states the same four conditions in SQL; a test pins that the two agree.
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
    public static IQueryable<BacklogMember> Members(MustaskDbContext context, BacklogScope scope)
    {
        var typeIds = scope.TypeIds.ToList();
        var stateIds = scope.ShownStateIds.ToList();
        var teamId = scope.TeamId;
        var levelId = scope.LevelId;

        return from item in context.WorkItems.AsNoTracking()
               where item.ProjectId == scope.ProjectId
                   && item.OwningTeamId == teamId
                   && typeIds.Contains(item.TypeId)
                   && stateIds.Contains(item.StateId)
               join place in context.Set<BacklogPlace>().AsNoTracking() on item.Id equals place.WorkItemId into placed
               from place in placed.DefaultIfEmpty()
               select new BacklogMember
               {
                   Item = item,
                   Place = place != null && place.TeamId == teamId && place.BacklogLevelId == levelId ? place.Position : null,
               };
    }

    /// <summary>
    /// feat-027 (api-contract § 1): the members of a page's tree set — the same four conditions over every level of
    /// <paramref name="scope"/> at once, in one statement — each with its place on the team's backlog of its own type's
    /// level, joined by the place's key as <see cref="Members"/> joins it.
    /// </summary>
    public static IQueryable<BacklogMember> TreeMembers(MustaskDbContext context, BacklogTreeScope scope)
    {
        var typeIds = scope.TypeIds.ToList();
        var stateIds = scope.ShownStateIds.ToList();
        var teamId = scope.TeamId;

        return from item in context.WorkItems.AsNoTracking()
               where item.ProjectId == scope.ProjectId
                   && item.OwningTeamId == teamId
                   && typeIds.Contains(item.TypeId)
                   && stateIds.Contains(item.StateId)
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
