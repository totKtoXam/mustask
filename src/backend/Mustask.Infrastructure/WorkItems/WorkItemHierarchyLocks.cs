using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// The one way the project's hierarchy lock is taken (<see cref="WorkItemHierarchyLock"/>; ADR-0028 rule 8 step 5, ADR-0030 rule 5):
/// every parent write (a save that carries a parent, the parent route, a creation beneath an item), every purge, and — since the
/// mod-005 merge review, QD-1 — every team clean-up (an unlink, a team's deletion) takes it <b>before</b> the work items it locks.
/// A parent write locks its child, then its new and its stored parent, in that order and not by id; a clean-up locks the items a team
/// owns by id. With no lock in common first, a parent of a lower id and a child of a higher one that one team owns were held
/// crosswise (<c>40P01</c>, a 500 for the clean-up); with the hierarchy lock first, the second of the two queues at it, holding no item.
/// </summary>
/// <remarks>
/// Two statements whatever the number of projects: the rows a project has not got yet are inserted, in project-id order, and then
/// all of them are locked <c>FOR UPDATE</c> in one statement ordered by project id — the same order for every caller that takes
/// several (a team's deletion takes one per linked project), and the database's own order, never the client's. A concurrent first
/// writer of a project waits at the insert on the other's uncommitted key and then does nothing.
/// </remarks>
internal static class WorkItemHierarchyLocks
{
    public static async Task AcquireAsync(MustaskDbContext context, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return;
        }

        var ids = projectIds.Distinct().ToArray();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO work_item_hierarchy_locks (project_id)
             SELECT project_id FROM unnest({ids}) AS project_id ORDER BY project_id
             ON CONFLICT (project_id) DO NOTHING
             """,
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_item_hierarchy_locks WHERE project_id = ANY({ids}) ORDER BY project_id FOR UPDATE",
            cancellationToken);
    }
}
