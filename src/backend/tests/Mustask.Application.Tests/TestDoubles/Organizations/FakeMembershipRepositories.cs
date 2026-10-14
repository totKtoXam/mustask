using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.TestDoubles.Organizations;

/// <summary>In-memory <see cref="IMembershipRepository"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeMembershipRepository(InMemoryOrganizations store) : IMembershipRepository
{
    /// <summary>
    /// Runs just before a member's row is locked, once: a test uses it to make another administrator's committed change land
    /// between the handler's pre-check and its lock (the window a real lock closes, plan-backend § 3.8).
    /// </summary>
    public Action? BeforeNextLockMember { get; set; }

    public Task<bool> ExistsAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(store.FindMembership(organizationId, userId) is not null);

    public Task<OrganizationRole?> FindRoleAsync(Guid organizationId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult<OrganizationRole?>(store.FindMembership(organizationId, userId)?.Role);

    public Task<OrganizationMembership?> LockMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken)
    {
        if (BeforeNextLockMember is { } before)
        {
            BeforeNextLockMember = null;
            before();
        }

        store.Record($"lock member {userId}");

        return Task.FromResult(store.FindMembership(organizationId, userId));
    }

    public void Add(OrganizationMembership membership) => store.Stage(membership);

    public Task<bool> DeleteAsync(Guid organizationId, string userId, CancellationToken cancellationToken)
    {
        store.Record($"delete member {userId}");

        return Task.FromResult(store.DeleteMembership(organizationId, userId));
    }

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();
}

/// <summary>In-memory <see cref="ITeamMembershipRepository"/> over <see cref="InMemoryOrganizations"/>.</summary>
public sealed class FakeTeamMembershipRepository(InMemoryOrganizations store) : ITeamMembershipRepository
{
    public Task<bool> ExistsAsync(Guid teamId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(store.TeamMemberships.Any(membership => membership.TeamId == teamId && membership.UserId == userId));

    public void Add(TeamMembership membership) => store.Stage(membership);

    public Task<bool> DeleteAsync(Guid teamId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(store.TeamMemberships.RemoveAll(membership => membership.TeamId == teamId && membership.UserId == userId) > 0);

    public Task EndAllAsync(Guid teamId, CancellationToken cancellationToken)
    {
        store.Record($"end team memberships {teamId}");
        store.TeamMemberships.RemoveAll(membership => membership.TeamId == teamId);

        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => store.SaveAsync();
}
