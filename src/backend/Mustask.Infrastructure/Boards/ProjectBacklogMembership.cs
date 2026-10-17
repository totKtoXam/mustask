using Mustask.Application.Abstractions.Boards;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// The one definition of what a project's backlog and board hold (feat-039 api-contract § 1, BR-PB-2), used by the
/// backlog read, the board read and the reorder alike. Three conditions, all in one <c>work_items</c> statement
/// served by <c>ix_work_items_project_id_type_id_area_id</c> on its (project, type) prefix:
/// <list type="number">
/// <item>the item belongs to the project (the caller has checked the project is reachable and of the organization);</item>
/// <item>its type is one of the level's;</item>
/// <item>its state is outside the Removed category.</item>
/// </list>
/// There is <b>no area condition and no team</b>: the item's area, the area's owning team or none do not narrow it,
/// and the team's area tree is never read. The place is the item's on the <i>project's</i> backlog
/// (<c>project_backlog_places</c>), apart from <see cref="BacklogMembership"/>'s team place.
/// </summary>
internal static class ProjectBacklogMembership
{
    /// <summary>
    /// The backlog's members with their places on it, composed on <see cref="IQueryable"/> so it runs in SQL. The place
    /// is joined by its key alone, the item, and kept only when it is on this backlog, as the team's membership does
    /// (a join on a unique key is planned as one lookup per item whatever the statistics say).
    /// </summary>
    public static IQueryable<BacklogMember> Members(MustaskDbContext context, ProjectBacklogScope scope)
    {
        var typeIds = scope.TypeIds.ToList();
        var stateIds = scope.ShownStateIds.ToList();
        var projectId = scope.ProjectId;
        var levelId = scope.LevelId;

        return from item in context.WorkItems.AsNoTracking()
               where item.ProjectId == projectId
                   && typeIds.Contains(item.TypeId)
                   && stateIds.Contains(item.StateId)
               join place in context.Set<ProjectBacklogPlace>().AsNoTracking() on item.Id equals place.WorkItemId into placed
               from place in placed.DefaultIfEmpty()
               select new BacklogMember
               {
                   Item = item,
                   Place = place != null && place.ProjectId == projectId && place.BacklogLevelId == levelId ? place.Position : null,
               };
    }

    /// <summary>
    /// The members of a page's tree set — the same three conditions over every level of <paramref name="scope"/> at
    /// once, in one statement — each with its place on the project's backlog of its own type's level.
    /// </summary>
    public static IQueryable<BacklogMember> TreeMembers(MustaskDbContext context, ProjectBacklogTreeScope scope)
    {
        var typeIds = scope.TypeIds.ToList();
        var stateIds = scope.ShownStateIds.ToList();
        var projectId = scope.ProjectId;

        return from item in context.WorkItems.AsNoTracking()
               where item.ProjectId == projectId
                   && typeIds.Contains(item.TypeId)
                   && stateIds.Contains(item.StateId)
               join type in context.WorkItemTypes.AsNoTracking() on item.TypeId equals type.Id
               join place in context.Set<ProjectBacklogPlace>().AsNoTracking() on item.Id equals place.WorkItemId into placed
               from place in placed.DefaultIfEmpty()
               select new BacklogMember
               {
                   Item = item,
                   Place = place != null && place.ProjectId == projectId && place.BacklogLevelId == type.BacklogLevelId ? place.Position : null,
               };
    }
}
