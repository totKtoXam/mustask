using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-016's test data, built without any endpoint so that no test depends on another use case (the way
/// <see cref="OrganizationsTestData"/> builds M1's structure): an organization membership or a team membership through the
/// repositories, a member of a given role with a token, a user in the user projection. The integration database is shared by the
/// collection and never emptied, so every user gets a fresh id.
/// </summary>
public static class MembershipTestData
{
    /// <summary>A member of an organization: the user's id (the token's subject) and a token that carries it.</summary>
    public sealed record TestMember(string Id, string Token);

    public static string NewUserId() => Guid.CreateVersion7().ToString();

    /// <summary>A membership of <paramref name="userId"/>, added by <paramref name="addedBy"/> (the test organization's creator by default).</summary>
    public static Task AddMemberAsync(
        MustaskApiFactory factory, Guid organizationId, string userId, OrganizationRole role = OrganizationRole.Member, string? addedBy = null) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var memberships = services.GetRequiredService<IMembershipRepository>();
            memberships.Add(OrganizationMembership.Add(
                organizationId, userId, role, addedBy ?? OrganizationsTestData.Creator, OrganizationsTestData.Now));
            await memberships.SaveAsync(ct);

            return true;
        });

    /// <summary>A team membership: the user must already be a member of the organization (BR-M1-10).</summary>
    public static Task AddTeamMemberAsync(MustaskApiFactory factory, Guid organizationId, Guid teamId, string userId) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var teamMemberships = services.GetRequiredService<ITeamMembershipRepository>();
            teamMemberships.Add(TeamMembership.Add(teamId, organizationId, userId));
            await teamMemberships.SaveAsync(ct);

            return true;
        });

    /// <summary>Removes an organization membership as the endpoint does (the cascade ends the user's team memberships).</summary>
    public static Task<bool> RemoveMemberAsync(MustaskApiFactory factory, Guid organizationId, string userId) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, (services, ct) =>
            services.GetRequiredService<IMembershipRepository>().DeleteAsync(organizationId, userId, ct));

    /// <summary>A fresh user who is a member of the organization with the role, and a token that carries their id as its subject.</summary>
    public static async Task<TestMember> NewMemberAsync(MustaskApiFactory factory, Guid organizationId, OrganizationRole role)
    {
        var id = NewUserId();
        await AddMemberAsync(factory, organizationId, id, role);

        return new TestMember(id, OrganizationsTokens.SignedIn(id));
    }

    /// <summary>The token of a user who is a member of nothing.</summary>
    public static string Outsider() => OrganizationsTokens.Stranger();

    /// <summary>The stored organization membership, or <c>null</c>.</summary>
    public static async Task<OrganizationMembership?> FindMembershipAsync(MustaskApiFactory factory, Guid organizationId, string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().OrganizationMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(membership => membership.OrganizationId == organizationId && membership.UserId == userId);
    }

    /// <summary>The ids of the users in a team, as stored.</summary>
    public static async Task<List<string>> TeamMemberIdsAsync(MustaskApiFactory factory, Guid teamId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.TeamId == teamId)
            .Select(membership => membership.UserId)
            .OrderBy(id => id)
            .ToListAsync();
    }

    /// <summary>Every team membership of a user in an organization, as (team, organization), straight from the table.</summary>
    public static async Task<List<(Guid TeamId, Guid OrganizationId)>> TeamMembershipsOfAsync(MustaskApiFactory factory, string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().TeamMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Select(membership => new { membership.TeamId, membership.OrganizationId })
            .ToListAsync();

        return rows.Select(row => (row.TeamId, row.OrganizationId)).ToList();
    }

    /// <summary>A user in the user projection (mod-014) under the given id: active unless the test says otherwise.</summary>
    public static Task ProjectUserAsync(
        MustaskApiFactory factory, string userId, string firstName, string lastName, string? email = null, bool active = true) =>
        UserDirectoryTestData.SeedAsync(factory, [UserDirectoryTestData.Push(userId, firstName, lastName, email, enabled: active)]);

    /// <summary>
    /// A token whose subject the projection knows (<see cref="ProjectUserAsync"/>) — an administrator of the organization with
    /// a name, as an organization's creator is once the projection has received them.
    /// </summary>
    public static async Task<TestMember> NewNamedMemberAsync(
        MustaskApiFactory factory, Guid organizationId, OrganizationRole role, string firstName, string lastName, string? email = null)
    {
        var member = await NewMemberAsync(factory, organizationId, role);
        await ProjectUserAsync(factory, member.Id, firstName, lastName, email);

        return member;
    }
}
