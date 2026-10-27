using Mustask.Infrastructure.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// ADR-0026 rule 3, computed in one place: an object is reachable when it and
/// every container above it are active. A container's removal hides what is
/// beneath it without touching it, and a restore brings back exactly what was
/// reachable before (US-2, US-3, BR-M1-20).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class ReachabilityTests(OrganizationsDatabaseFixture database)
{
    // US-2 · "deactivated": everything beneath becomes unreachable; "restored": it comes back as it was.
    [Fact]
    public async Task DeactivatedOrganization_HidesItsProjectsAndTeams_RestoreBringsThemBack()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var projectId = structure.Project.Id;

        Assert.Equal((true, 1, 2), await ReachAsync(organizationId, projectId));

        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", organizationId));
        Assert.Equal((false, 0, 0), await ReachAsync(organizationId, projectId));

        await database.ExecuteAsync("UPDATE organizations SET status = 'Active' WHERE id = @id", ("id", organizationId));
        Assert.Equal((true, 1, 2), await ReachAsync(organizationId, projectId));
    }

    // US-3 · "deactivated": the project leaves the active list, joins the deactivated one, and hides its teams and areas.
    [Fact]
    public async Task DeactivatedProject_IsListedSeparately_AndHidesWhatIsBeneath()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var projectId = structure.Project.Id;

        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", projectId));

        await using var context = database.NewContext();
        Assert.False(await context.ReachableProjects(organizationId).AnyAsync(project => project.Id == projectId));
        Assert.True(await context.DeactivatedProjectsOf(organizationId).AnyAsync(project => project.Id == projectId));
        Assert.Empty(await context.LinkedTeams(organizationId, projectId).ToListAsync());
    }

    // US-2 · "restored exactly as it was": a project deactivated on its own stays deactivated, and stays off both lists
    // while its organization is deactivated.
    [Fact]
    public async Task ProjectDeactivatedOnItsOwn_StaysDeactivatedAcrossTheOrganizationsRestore()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var projectId = structure.Project.Id;

        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", projectId));
        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", organizationId));

        await using (var context = database.NewContext())
        {
            Assert.False(await context.DeactivatedProjectsOf(organizationId).AnyAsync());
            Assert.False(await context.ReachableProjects(organizationId).AnyAsync());
        }

        await database.ExecuteAsync("UPDATE organizations SET status = 'Active' WHERE id = @id", ("id", organizationId));

        await using (var context = database.NewContext())
        {
            Assert.True(await context.DeactivatedProjectsOf(organizationId).AnyAsync(project => project.Id == projectId));
            Assert.False(await context.ReachableProjects(organizationId).AnyAsync(project => project.Id == projectId));
        }
    }

    // US-7 · "A deleted team answers not found"; BR-M1-22: a project is reachable only under its own organization.
    [Fact]
    public async Task DeletedTeam_AndProjectOfAnotherOrganization_AreNotReachable()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var projectId = structure.Project.Id;

        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", ("second", structure.SecondTeam.Id));

        await using var context = database.NewContext();
        Assert.Equal(
            [structure.FirstTeam.Id],
            await context.LinkedTeams(organizationId, projectId).Select(team => team.Id).ToListAsync());
        Assert.False(await context.ReachableProjects(other.Organization.Id).AnyAsync(project => project.Id == projectId));
        Assert.Empty(await context.LinkedTeams(other.Organization.Id, projectId).ToListAsync());
    }

    // feat-028 BR-OL-1, BR-OL-4 · a team is the organization's: it is hidden by its OWN status and its organization's, never by a
    // project. A deactivated project hides its link from the team's projects without removing the link or the team.
    [Fact]
    public async Task ATeam_IsHiddenByItsOwnStatusAndItsOrganization_NotByAProject_ADeactivatedProjectHidesItsLinkOnly()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var projectId = structure.Project.Id;
        var teamId = structure.FirstTeam.Id;

        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", projectId));

        await using (var context = database.NewContext())
        {
            Assert.Contains(teamId, await context.ActiveTeamsOf(organizationId).Select(team => team.Id).ToListAsync());
            Assert.Empty(await context.LinkedProjects(organizationId, teamId).ToListAsync());
            Assert.True(await context.ProjectTeams.AnyAsync(link => link.ProjectId == projectId && link.TeamId == teamId));
        }

        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", organizationId));

        await using (var context = database.NewContext())
        {
            Assert.Empty(await context.ActiveTeamsOf(organizationId).ToListAsync());
        }
    }

    private async Task<(bool Organization, int Projects, int Teams)> ReachAsync(Guid organizationId, Guid projectId)
    {
        await using var context = database.NewContext();

        return (
            await context.ActiveOrganizations().AnyAsync(organization => organization.Id == organizationId),
            await context.ReachableProjects(organizationId).CountAsync(),
            await context.LinkedTeams(organizationId, projectId).CountAsync());
    }
}
