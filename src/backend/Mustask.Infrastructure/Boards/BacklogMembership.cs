using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Organizations;
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
/// The one definition of what a team's backlog and board hold (spec § Model,
/// BR-M4-5), used by the backlog read, the board read and the reorder alike:
/// <list type="number">
/// <item>the item belongs to the project (the caller has checked the team is an active team of it);</item>
/// <item>its type is one of the level's;</item>
/// <item>its area resolves to the team — mod-002's own rule, <see cref="AreaTree.ResolveOwner"/>, run once
/// over the project's area nodes (tens to hundreds of rows), never per item;</item>
/// <item>its state is outside the Removed category.</item>
/// </list>
/// Conditions 1, 2 and 4 and the team's area set go into one <c>work_items</c>
/// statement, served by <c>ix_work_items_project_id_type_id_area_id</c>, which
/// left-joins the item's place on this backlog by the place's key.
/// </summary>
internal static class BacklogMembership
{
    /// <summary>The project's area tree as stored now, the input of <see cref="TeamAreas"/>.</summary>
    public static async Task<AreaTree> AreaTreeAsync(MustaskDbContext context, Guid projectId, CancellationToken cancellationToken)
    {
        var nodes = await context.Areas
            .AsNoTracking()
            .Where(area => area.ProjectId == projectId)
            .Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId))
            .ToListAsync(cancellationToken);

        return new AreaTree(nodes);
    }

    /// <summary>Every area whose resolved owner is <paramref name="teamId"/> (condition 3); empty for a team that owns none.</summary>
    public static IReadOnlyList<Guid> TeamAreas(AreaTree tree, Guid teamId) =>
        tree.PreOrder().Where(node => tree.ResolveOwner(node.Id).TeamId == teamId).Select(node => node.Id).ToList();

    /// <summary>
    /// The backlog's members with their places on it, composed on
    /// <see cref="IQueryable"/> so it runs in SQL. The place is joined by its key
    /// alone, the item, and kept only when it is on this backlog: a join on a
    /// unique key is planned as one lookup per item whatever the statistics say,
    /// whereas filtering the places by team and level first lets a planner with
    /// stale statistics (a backlog placed a moment ago) expect one row and scan
    /// all of them for every item.
    /// </summary>
    public static IQueryable<BacklogMember> Members(MustaskDbContext context, BacklogScope scope, IReadOnlyCollection<Guid> teamAreas)
    {
        var typeIds = scope.TypeIds.ToList();
        var stateIds = scope.ShownStateIds.ToList();
        var areaIds = teamAreas.ToList();
        var teamId = scope.TeamId;
        var levelId = scope.LevelId;

        return from item in context.WorkItems.AsNoTracking()
               where item.ProjectId == scope.ProjectId
                   && typeIds.Contains(item.TypeId)
                   && areaIds.Contains(item.AreaId)
                   && stateIds.Contains(item.StateId)
               join place in context.Set<BacklogPlace>().AsNoTracking() on item.Id equals place.WorkItemId into placed
               from place in placed.DefaultIfEmpty()
               select new BacklogMember
               {
                   Item = item,
                   Place = place != null && place.TeamId == teamId && place.BacklogLevelId == levelId ? place.Position : null,
               };
    }
}
