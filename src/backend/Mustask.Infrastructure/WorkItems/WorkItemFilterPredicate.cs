using Mustask.Domain.WorkItems;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// The one predicate of the team filter (mod-017 BR-OT-8, api-contract § 3): the project's list, backlog and board all narrow
/// their items through it, so the three surfaces cannot disagree for any combination. It is composed on <see cref="IQueryable"/>
/// and goes into the same statement as the rest of the query — a filter adds none. Boards' memberships use it too: Boards'
/// infrastructure already uses WorkItems' (<see cref="WorkItemRefs"/>).
/// </summary>
internal static class WorkItemFilterPredicate
{
    /// <summary>
    /// <paramref name="items"/> narrowed by <paramref name="filter"/>: an item matches when it is owned by any chosen team, or
    /// by none when <c>none</c> is chosen. No filter leaves the items as they are; a filter that is set but names no team the
    /// item can be owned by selects nothing (a malformed value, an unknown, deleted, other-project or foreign id).
    /// </summary>
    public static IQueryable<WorkItem> Apply(IQueryable<WorkItem> items, WorkItemFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!filter.TeamsSet)
        {
            return items;
        }

        var teamIds = filter.TeamIds.ToList();
        var noTeam = filter.NoTeam;

        return items.Where(item =>
            (item.OwningTeamId != null && teamIds.Contains(item.OwningTeamId.Value))
            || (noTeam && item.OwningTeamId == null));
    }
}
