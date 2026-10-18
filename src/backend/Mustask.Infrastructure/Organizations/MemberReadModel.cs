using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Organizations;

/// <summary>
/// <see cref="IMemberReadModel"/> (mod-016 plan-backend § 3.4, § 3.7): the memberships in one statement, then the people named
/// in them through <see cref="IUserDirectory"/> — never the projection's tables, never Keycloak — in
/// <c>ceil(ids / 500)</c> statements (<see cref="UserDirectoryBatches"/>, BR-UP-17). The lists are small enough to be read
/// whole (up to about 500 rows), so there is no paging; the order is put on them in memory over exactly the rows read
/// (backend.md: finishing off a selection already narrowed is assembling a projection, not post-filtering).
/// </summary>
internal sealed class MemberReadModel(MustaskDbContext context, IUserDirectory directory) : IMemberReadModel
{
    public async Task<IReadOnlyList<MemberView>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = await context.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId)
            .Select(membership => new StoredMember(
                membership.UserId, membership.Role, membership.AddedBy, membership.AddedAt, membership.RoleChangedBy, membership.RoleChangedAt))
            .ToListAsync(cancellationToken);

        return await ViewsAsync(rows, cancellationToken);
    }

    public async Task<MemberView?> FindAsync(Guid organizationId, string userId, CancellationToken cancellationToken)
    {
        var rows = await context.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId && membership.UserId == userId)
            .Select(membership => new StoredMember(
                membership.UserId, membership.Role, membership.AddedBy, membership.AddedAt, membership.RoleChangedBy, membership.RoleChangedAt))
            .ToListAsync(cancellationToken);

        return rows.Count == 0 ? null : (await ViewsAsync(rows, cancellationToken))[0];
    }

    public async Task<IReadOnlyList<UserRefView>> ListTeamMembersAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var ids = await context.TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        var lookup = await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken);

        return UserRefOrder.Order(ids, id => id, lookup.Known)
            .Select(id => UserRefView.Of(id, lookup.Known))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListMemberIdsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await context.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

    public Task<bool> IsMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        context.OrganizationMemberships
            .AsNoTracking()
            .AnyAsync(membership => membership.OrganizationId == organizationId && membership.UserId == userId, cancellationToken);

    public async Task<IReadOnlySet<string>> FilterMembersAsync(
        Guid organizationId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
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

    public async Task<int> CountOtherKnownActiveAdministratorsAsync(Guid organizationId, string exceptUserId, CancellationToken cancellationToken)
    {
        var administrators = await context.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId
                && membership.Role == OrganizationRole.Admin
                && membership.UserId != exceptUserId)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        if (administrators.Count == 0)
        {
            return 0;
        }

        // Unknown to the projection counts for nothing (BR-M1-32); so does a disabled or deleted account.
        var lookup = await UserDirectoryBatches.LookupAllAsync(directory, administrators, cancellationToken);

        return lookup.Known.Values.Count(user => user.IsActive);
    }

    /// <summary>
    /// The views of <paramref name="rows"/>: one lookup of every member id and every adder and role-changer id (they may no
    /// longer be members), the members ordered as api-contract § 0.5 says, an unknown person kept with their id and no name.
    /// </summary>
    private async Task<IReadOnlyList<MemberView>> ViewsAsync(List<StoredMember> rows, CancellationToken cancellationToken)
    {
        var ids = rows.SelectMany(row => new[] { row.UserId, row.AddedBy, row.RoleChangedBy }).OfType<string>();
        var known = (await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken)).Known;

        return UserRefOrder.Order(rows, row => row.UserId, known)
            .Select(row => new MemberView(
                UserRefView.Of(row.UserId, known),
                OrganizationRoles.ToCode(row.Role),
                UserRefView.Of(row.AddedBy, known),
                row.AddedAt,
                row.RoleChangedBy is { } changedBy ? UserRefView.Of(changedBy, known) : null,
                row.RoleChangedAt))
            .ToList();
    }

    private sealed record StoredMember(
        string UserId,
        OrganizationRole Role,
        string AddedBy,
        DateTimeOffset AddedAt,
        string? RoleChangedBy,
        DateTimeOffset? RoleChangedAt);
}
