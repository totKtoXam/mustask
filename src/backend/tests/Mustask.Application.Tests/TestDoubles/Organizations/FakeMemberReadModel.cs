using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.Identity;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>
/// In-memory <see cref="IMemberReadModel"/> over <see cref="InMemoryOrganizations"/> and a <see cref="FakeUserDirectory"/>: the
/// same shape as the real one — the memberships, then the people through the directory in batches of 500, ordered by
/// <see cref="UserRefOrder"/>.
/// </summary>
public sealed class FakeMemberReadModel(InMemoryOrganizations store, IUserDirectory directory) : IMemberReadModel
{
    public async Task<IReadOnlyList<MemberView>> ListAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = store.Memberships.Where(membership => membership.OrganizationId == organizationId).ToList();

        return await ViewsAsync(rows, cancellationToken);
    }

    public async Task<MemberView?> FindAsync(Guid organizationId, string userId, CancellationToken cancellationToken)
    {
        return store.FindMembership(organizationId, userId) is { } membership
            ? (await ViewsAsync([membership], cancellationToken))[0]
            : null;
    }

    public async Task<IReadOnlyList<UserRefView>> ListTeamMembersAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var ids = store.TeamMemberships.Where(membership => membership.TeamId == teamId).Select(membership => membership.UserId).ToList();
        var known = (await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken)).Known;

        return UserRefOrder.Order(ids, id => id, known).Select(id => UserRefView.Of(id, known)).ToList();
    }

    public Task<IReadOnlyList<string>> ListMemberIdsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(
            store.Memberships.Where(membership => membership.OrganizationId == organizationId).Select(membership => membership.UserId).ToList());

    public Task<bool> IsMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(store.FindMembership(organizationId, userId) is not null);

    public Task<IReadOnlySet<string>> FilterMembersAsync(Guid organizationId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(
            store.Memberships.Where(membership => membership.OrganizationId == organizationId && userIds.Contains(membership.UserId))
                .Select(membership => membership.UserId)
                .ToHashSet(StringComparer.Ordinal));

    public async Task<int> CountOtherKnownActiveAdministratorsAsync(Guid organizationId, string exceptUserId, CancellationToken cancellationToken)
    {
        var administrators = store.Memberships
            .Where(membership => membership.OrganizationId == organizationId && membership.Role == OrganizationRole.Admin && membership.UserId != exceptUserId)
            .Select(membership => membership.UserId)
            .ToList();

        var lookup = await UserDirectoryBatches.LookupAllAsync(directory, administrators, cancellationToken);

        return lookup.Known.Values.Count(user => user.IsActive);
    }

    private async Task<IReadOnlyList<MemberView>> ViewsAsync(List<Mustask.Domain.Organizations.OrganizationMembership> rows, CancellationToken cancellationToken)
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
}

/// <summary>A scripted <see cref="IOrganizationAccess"/>: the role is whatever the test says, and its bundle is the real one.</summary>
public sealed class FakeOrganizationAccess(OrganizationRole role) : IOrganizationAccess
{
    public OrganizationRole Role { get; set; } = role;

    public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);
}
