using Mustask.Domain.WorkItems;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// The one predicate of the shared filter (mod-017 BR-OT-8, feat-040 BR-TS-8, api-contract § 3): the project's list, backlog
/// and board narrow their items by the team dimension through it, a team's backlog and board by the project dimension, so the
/// surfaces cannot disagree for any combination. It is composed on <see cref="IQueryable"/> and goes into the same statement
/// as the rest of the query — a filter adds none. Boards' memberships use it too: Boards' infrastructure already uses
/// WorkItems' (<see cref="WorkItemRefs"/>).
/// </summary>
internal static class WorkItemFilterPredicate
{
    /// <summary>
    /// <paramref name="items"/> narrowed by <paramref name="filter"/>: an item matches when it is owned by any chosen team, or
    /// by none when <c>none</c> is chosen, and (a team's views) when its project is any chosen project. No filter leaves the
    /// items as they are; a dimension that is set but names nothing the item can have selects nothing (a malformed value, an
    /// unknown, deleted, unlinked or foreign id).
    /// </summary>
    public static IQueryable<WorkItem> Apply(IQueryable<WorkItem> items, WorkItemFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.TeamsSet)
        {
            var teamIds = filter.TeamIds.ToList();
            var noTeam = filter.NoTeam;

            items = items.Where(item =>
                (item.OwningTeamId != null && teamIds.Contains(item.OwningTeamId.Value))
                || (noTeam && item.OwningTeamId == null));
        }

        if (filter.ProjectsSet)
        {
            var projectIds = filter.ProjectIds.ToList();

            items = items.Where(item => projectIds.Contains(item.ProjectId));
        }

        return items;
    }
}
