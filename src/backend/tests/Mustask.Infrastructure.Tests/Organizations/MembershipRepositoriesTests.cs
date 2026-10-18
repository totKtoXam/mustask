using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Tests.Process;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// mod-016 plan-backend § 3.4, § 3.11: the membership repositories against a real PostgreSQL — the locks, the delete that cascades
/// (BR-M1-36), the races on the keys answered with the pre-checks' codes (<c>OrganizationsSaveChanges</c>), and the team list's
/// <c>isMember</c> flag (BR-M1-27) in the same statement as the list.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class MembershipRepositoriesTests(OrganizationsDatabaseFixture database)
{
    // BR-M1-5 (NFR Concurrency): the loser of two adds of one user hears already_member from the primary key.
    [Fact]
    public async Task Save_ADuplicateMembership_IsAlreadyMember()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-repo");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => InUnitOfWorkAsync(async (scope, ct) =>
        {
            var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
            memberships.Add(OrganizationMembership.Add(structure.Organization.Id, "u-repo", OrganizationRole.Viewer, "adm", OrganizationsDatabaseFixture.Now));
            await memberships.SaveAsync(ct);

            return true;
        }));

        Assert.Equal(OrganizationErrorCodes.MembershipAlreadyMember, refusal.Code);
    }

    // The same user twice in one team: member_exists from the primary key.
    [Fact]
    public async Task Save_ADuplicateTeamMembership_IsMemberExists()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-repo");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-repo");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => InUnitOfWorkAsync(async (scope, ct) =>
        {
            var teamMemberships = scope.ServiceProvider.GetRequiredService<ITeamMembershipRepository>();
            teamMemberships.Add(TeamMembership.Add(structure.SecondTeam.Id, structure.Organization.Id, "u-repo"));
            await teamMemberships.SaveAsync(ct);

            return true;
        }));

        Assert.Equal(OrganizationErrorCodes.TeamMemberExists, refusal.Code);
    }

    // BR-M1-10 (NFR Concurrency): the organization membership went before the team membership was written — the composite key.
    [Fact]
    public async Task Save_ATeamMembershipWithoutItsOrganizationMembership_IsMemberNotInOrganization()
    {
        var structure = await database.CreateStructureAsync();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => InUnitOfWorkAsync(async (scope, ct) =>
        {
            var teamMemberships = scope.ServiceProvider.GetRequiredService<ITeamMembershipRepository>();
            teamMemberships.Add(TeamMembership.Add(structure.SecondTeam.Id, structure.Organization.Id, "u-removed"));
            await teamMemberships.SaveAsync(ct);

            return true;
        }));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotInOrganization, refusal.Code);
    }

    // BR-M1-36 · the raw delete of an organization membership answers whether a row went, and the cascade ends the team memberships.
    [Fact]
    public async Task Delete_AMembership_AnswersWhetherARowWent_AndEndsTheUsersTeamMemberships()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-repo");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-repo");

        var first = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<IMembershipRepository>().DeleteAsync(structure.Organization.Id, "u-repo", ct));
        var second = await InUnitOfWorkAsync((scope, ct) =>
            scope.ServiceProvider.GetRequiredService<IMembershipRepository>().DeleteAsync(structure.Organization.Id, "u-repo", ct));

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(
            "0",
            Assert.Single(await database.QueryAsync(
                "SELECT count(*) FROM team_memberships WHERE organization_id = @org AND user_id = 'u-repo'",
                ("org", structure.Organization.Id))));
    }

    // The member's row is locked FOR UPDATE and loaded with the regular tracking query; an unknown user is null.
    [Fact]
    public async Task LockMember_ReturnsTheTrackedMembership_OrNull()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-repo", OrganizationRole.Viewer);

        var result = await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
            var found = await memberships.LockMemberAsync(structure.Organization.Id, "u-repo", ct);
            var missing = await memberships.LockMemberAsync(structure.Organization.Id, "nobody", ct);
            var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

            return (Role: found?.Role, Missing: missing, Tracked: context.ChangeTracker.Entries<OrganizationMembership>().Count());
        });

        Assert.Equal(OrganizationRole.Viewer, result.Role);
        Assert.Null(result.Missing);
        Assert.Equal(1, result.Tracked);
    }

    [Fact]
    public async Task ExistsAsync_IsExactToTheOrganizationAndTheUser()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-repo");
        await database.AddMemberAsync(other.Organization.Id, "u-other");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-repo");

        var result = await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
            var teamMemberships = scope.ServiceProvider.GetRequiredService<ITeamMembershipRepository>();

            return new[]
            {
                await memberships.ExistsAsync(structure.Organization.Id, "u-repo", ct),
                await memberships.ExistsAsync(structure.Organization.Id, "u-other", ct),
                await memberships.ExistsAsync(other.Organization.Id, "u-repo", ct),
                await teamMemberships.ExistsAsync(structure.SecondTeam.Id, "u-repo", ct),
                await teamMemberships.ExistsAsync(structure.FirstTeam.Id, "u-repo", ct),
            };
        });

        Assert.Equal([true, false, false, true, false], result);
    }

    // BE-SEC-1 · the role read again under the lock: the stored role of the user in that organization, null for a non-member, and
    // nothing tracked (the caller's row is read, not locked or loaded for a change).
    [Fact]
    public async Task FindRole_IsTheStoredRoleOfTheUserHere_NullForANonMember_AndTracksNothing()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-admin", OrganizationRole.Admin);
        await database.AddMemberAsync(structure.Organization.Id, "u-viewer", OrganizationRole.Viewer);
        await database.AddMemberAsync(other.Organization.Id, "u-elsewhere", OrganizationRole.Member);

        var result = await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
            var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

            return (
                Roles: new[]
                {
                    await memberships.FindRoleAsync(structure.Organization.Id, "u-admin", ct),
                    await memberships.FindRoleAsync(structure.Organization.Id, "u-viewer", ct),
                    await memberships.FindRoleAsync(structure.Organization.Id, "u-elsewhere", ct),
                    await memberships.FindRoleAsync(Guid.NewGuid(), "u-admin", ct),
                },
                Tracked: context.ChangeTracker.Entries<OrganizationMembership>().Count());
        });

        Assert.Equal([OrganizationRole.Admin, OrganizationRole.Viewer, null, null], result.Roles);
        Assert.Equal(0, result.Tracked);
    }

    // BR-M1-18, BR-M1-36 · the end of a team's memberships: all of that team's, none of another team's.
    [Fact]
    public async Task EndAll_RemovesEveryMembershipOfTheTeam_OnlyThatTeams()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-1");
        await database.AddMemberAsync(structure.Organization.Id, "u-2");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-1");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-2");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.FirstTeam.Id, "u-1");

        await InUnitOfWorkAsync(async (scope, ct) =>
        {
            await scope.ServiceProvider.GetRequiredService<ITeamMembershipRepository>().EndAllAsync(structure.SecondTeam.Id, ct);

            return true;
        });

        var remaining = await database.QueryAsync(
            "SELECT team_id::text || '/' || user_id FROM team_memberships WHERE organization_id = @org", ("org", structure.Organization.Id));
        Assert.Equal([$"{structure.FirstTeam.Id}/u-1"], remaining);
    }

    [Fact]
    public async Task DeleteTeamMember_AnswersWhetherARowWent()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-1");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-1");

        var result = await InUnitOfWorkAsync(async (scope, ct) =>
        {
            var teamMemberships = scope.ServiceProvider.GetRequiredService<ITeamMembershipRepository>();

            return new[]
            {
                await teamMemberships.DeleteAsync(structure.FirstTeam.Id, "u-1", ct),
                await teamMemberships.DeleteAsync(structure.SecondTeam.Id, "u-1", ct),
                await teamMemberships.DeleteAsync(structure.SecondTeam.Id, "u-1", ct),
            };
        });

        Assert.Equal([false, true, false], result);
    }

    // mod-016 US-7 · "Choosing a project selects the user's own team" (BR-M1-27): each team says whether the CALLER is in it; the
    // flag comes with the list in one statement, whatever the number of teams.
    [Fact]
    public async Task TeamList_IsMember_FollowsTheCallersTeamMemberships_InOneStatement()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        var analytics = Team.Create(structure.Organization.Id, OrganizationsDatabaseFixture.Name("Аналитика"));
        await using (var context = database.NewContext())
        {
            context.Teams.Add(analytics);
            context.ProjectTeams.Add(ProjectTeam.Link(structure.Organization.Id, projectId, analytics.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            await context.SaveChangesAsync();
        }

        await database.AddMemberAsync(structure.Organization.Id, "u-me");
        await database.AddMemberAsync(structure.Organization.Id, "u-you");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-me");
        await database.AddTeamMemberAsync(structure.Organization.Id, analytics.Id, "u-you");

        var counter = new CommandCountingInterceptor();
        await using var provider = database.BuildServices(counter);
        await using var scope = provider.CreateAsyncScope();
        var readModel = scope.ServiceProvider.GetRequiredService<ITeamReadModel>();

        var mine = await readModel.ListLinkedAsync(structure.Organization.Id, projectId, "u-me", CancellationToken.None);
        var statements = counter.Commands.Count;
        var yours = await readModel.ListLinkedAsync(structure.Organization.Id, projectId, "u-you", CancellationToken.None);
        var nobody = await readModel.ListLinkedAsync(structure.Organization.Id, projectId, "u-nobody", CancellationToken.None);

        Assert.Equal(1, statements);
        Assert.Equal([("Аналитика", false), ("Платформа", true), ("Пример проекта", false)], mine.Select(team => (team.Name, team.IsMember)));
        Assert.Equal([("Аналитика", true), ("Платформа", false), ("Пример проекта", false)], yours.Select(team => (team.Name, team.IsMember)));
        Assert.All(nobody, team => Assert.False(team.IsMember));
    }

    // The single-team read does not carry the flag (api-contract § 3.3).
    [Fact]
    public async Task TeamFind_NeverCarriesTheFlag()
    {
        var structure = await database.CreateStructureAsync();
        await database.AddMemberAsync(structure.Organization.Id, "u-me");
        await database.AddTeamMemberAsync(structure.Organization.Id, structure.SecondTeam.Id, "u-me");
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();

        var row = await scope.ServiceProvider.GetRequiredService<ITeamReadModel>()
            .FindLinkedAsync(structure.Organization.Id, structure.Project.Project.Id, structure.SecondTeam.Id, CancellationToken.None);

        Assert.False(row!.IsMember);
    }

    private async Task<T> InUnitOfWorkAsync<T>(Func<IServiceScope, CancellationToken, Task<T>> work)
    {
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return await unitOfWork.ExecuteInTransactionAsync(ct => work(scope, ct), CancellationToken.None);
    }
}
