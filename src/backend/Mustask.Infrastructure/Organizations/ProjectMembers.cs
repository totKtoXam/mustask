using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// The one statement the work-item reads add for mod-016 BR-M1-26: which of the assignees a page names are, right now,
/// members of the project's organization (any role). The list, the item, the backlog and the board all go through the two
/// read models that use it, and the save and create answers go through the item read.
/// </summary>
internal static class ProjectMembers
{
    /// <summary>
    /// The ids among <paramref name="userIds"/> that hold a membership in the organization of project
    /// <paramref name="projectId"/>. One statement; no statement when there is nothing to ask.
    /// </summary>
    public static async Task<HashSet<string>> MemberIdsAsync(
        MustaskDbContext context, Guid projectId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ids = userIds.ToArray();

        var members = await (
                from membership in context.OrganizationMemberships.AsNoTracking()
                join project in context.Projects on membership.OrganizationId equals project.OrganizationId
                where project.Id == projectId && ids.Contains(membership.UserId)
                select membership.UserId)
            .ToListAsync(cancellationToken);

        return members.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The ids among <paramref name="userIds"/> that hold a membership in the organization <paramref name="organizationId"/>
    /// itself (feat-040 plan-backend PD-12): a team's view holds the rows of several projects, so it asks of the organization
    /// they all share, not of one project. One statement; no statement when there is nothing to ask.
    /// </summary>
    public static async Task<HashSet<string>> MemberIdsOfOrganizationAsync(
        MustaskDbContext context, Guid organizationId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ids = userIds.ToArray();

        var members = await context.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId && ids.Contains(membership.UserId))
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        return members.ToHashSet(StringComparer.Ordinal);
    }
}
