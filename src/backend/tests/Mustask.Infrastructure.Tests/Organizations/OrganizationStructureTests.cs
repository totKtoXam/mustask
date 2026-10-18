using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Infrastructure.Tests.Organizations.OrganizationsDatabaseFixture;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// api-contract § 7, feat-028 api-contract § 6: the reads M1 offers other modules. By-id reads return the object with its status,
/// reachable or not (ADR-0026 rule 4), and <b>take the organization</b>: <c>null</c> means that no such object ever existed or that it
/// belongs to another organization (BR-OL-1, mod-017 S-2). Rewritten over the organization-scoped reads (P-1: mod-002 api-contract § 7
/// → BR-OL-1).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OrganizationStructureTests(OrganizationsDatabaseFixture database)
{
    // mod-016 (BR-M1-21) reads the creator; US-1 · "created with its process".
    [Fact]
    public async Task FindOrganization_ReturnsStatusProcessAndCreator_EvenWhenDeactivated()
    {
        var structure = await database.CreateStructureAsync();
        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", structure.Organization.Id));

        var found = await ReadAsync(seam => seam.FindOrganizationAsync(structure.Organization.Id, CancellationToken.None));

        Assert.Equal(
            new OrganizationInfo(
                structure.Organization.Id,
                structure.Organization.Name,
                OrganizationStatus.Deactivated,
                structure.Organization.ProcessId,
                Creator,
                Now),
            found);
    }

    // US-6 · "Other modules get the same answer": the project, its root and its reachability. mod-017 BR-OT-4: a project with two
    // linked teams and none named has no default team.
    [Fact]
    public async Task FindProject_ReturnsTheDefaultTeamRootAndReachability()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Project.Id);

        var reachable = await ReadAsync(seam => seam.FindProjectAsync(organizationId, projectId, CancellationToken.None));
        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", organizationId));
        var underDeactivated = await ReadAsync(seam => seam.FindProjectAsync(organizationId, projectId, CancellationToken.None));

        Assert.Equal(
            new ProjectInfo(
                projectId,
                organizationId,
                "Пример проекта",
                "PRM",
                ProjectStatus.Active,
                IsReachable: true,
                DefaultTeamId: null,
                structure.Project.Root.Id,
                Creator,
                Now),
            reachable);
        Assert.Equal(ProjectStatus.Active, underDeactivated!.Status);
        Assert.False(underDeactivated.IsReachable);
    }

    // mod-017 BR-OT-4, feat-028 BR-OL-7 · the one definition of the default over the LINKS: the named team; the only linked team when
    // none is named; none when there are several and none is named; a named team that was deleted is no longer named; a team that
    // is not linked is no default.
    [Fact]
    public async Task FindProject_DefaultTeam_NamedOnlyTeamNone_AndANamedTeamThatWasDeleted()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Project.Id);
        var (first, second) = (structure.FirstTeam.Id, structure.SecondTeam.Id);

        Assert.Null((await ReadAsync(seam => seam.FindProjectAsync(organizationId, projectId, CancellationToken.None)))!.DefaultTeamId);

        await database.ExecuteAsync("UPDATE projects SET default_team_id = @team WHERE id = @id", ("team", second), ("id", projectId));
        Assert.Equal(second, (await ReadAsync(seam => seam.FindProjectAsync(organizationId, projectId, CancellationToken.None)))!.DefaultTeamId);

        // The deletion clears the setting in its own operation; this read also guards a stale one: a named team that is not active
        // is no longer named.
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", ("second", second));
        Assert.Equal(first, (await ReadAsync(seam => seam.FindProjectAsync(organizationId, projectId, CancellationToken.None)))!.DefaultTeamId);

        await database.ExecuteAsync("UPDATE projects SET default_team_id = NULL WHERE id = @id", ("id", projectId));
        Assert.Equal(first, (await ReadAsync(seam => seam.FindProjectAsync(organizationId, projectId, CancellationToken.None)))!.DefaultTeamId);
    }

    // US-3 · "the mark follows the links": a project with no link has no default team at all.
    [Fact]
    public async Task FindProject_WithNoLink_HasNoDefaultTeam()
    {
        var structure = await database.CreateStructureAsync();
        var bare = await database.AddProjectAsync(structure.Organization.Id, "Без команды", "BRE");

        var found = await ReadAsync(seam => seam.FindProjectAsync(structure.Organization.Id, bare.Project.Id, CancellationToken.None));

        Assert.Null(found!.DefaultTeamId);
    }

    // US-7 · "Other modules read the deleted status" (doc-010 PR-9): Deleted, never null and never active. mod-017: a team is a name
    // and a status; feat-028: the organization's, with no project.
    [Fact]
    public async Task FindTeam_DeletedTeam_AnswersDeleted_ListLinkedTeamsLeavesItOut()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId, secondId) = (structure.Organization.Id, structure.Project.Project.Id, structure.SecondTeam.Id);

        var before = await ReadAsync(seam => seam.FindTeamAsync(organizationId, secondId, CancellationToken.None));
        await database.ExecuteAsync("UPDATE teams SET status = 'Deleted' WHERE id = @second", ("second", secondId));
        var after = await ReadAsync(seam => seam.FindTeamAsync(organizationId, secondId, CancellationToken.None));
        var linked = await ReadAsync(seam => seam.ListLinkedTeamsAsync(organizationId, projectId, CancellationToken.None));

        Assert.Equal(new TeamInfo(secondId, organizationId, "Платформа", TeamStatus.Active, true), before);
        Assert.Equal(new TeamInfo(secondId, organizationId, "Платформа", TeamStatus.Deleted, false), after);
        Assert.Equal([structure.FirstTeam.Id], linked.Select(team => team.Id));
    }

    // mod-017 BR-OT-11: the linked teams of a project come by name in Russian collation, letter case not deciding, then by id.
    [Fact]
    public async Task ListLinkedTeams_ByName_NoTeamFirst_OnlyTheLinkedOnes()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Project.Id);
        var later = Team.Create(organizationId, Name("яблоко"));
        var earlier = Team.Create(organizationId, Name("Аналитика"));
        var unlinked = Team.Create(organizationId, Name("Не связана"));
        await using (var context = database.NewContext())
        {
            context.Teams.AddRange(later, earlier, unlinked);
            context.ProjectTeams.AddRange(
                ProjectTeam.Link(organizationId, projectId, later.Id, Creator, Now),
                ProjectTeam.Link(organizationId, projectId, earlier.Id, Creator, Now));
            await context.SaveChangesAsync();
        }

        var linked = await ReadAsync(seam => seam.ListLinkedTeamsAsync(organizationId, projectId, CancellationToken.None));

        Assert.Equal(["Аналитика", "Платформа", "Пример проекта", "яблоко"], linked.Select(team => team.Name));
    }

    // feat-028 BR-OL-6 · FindProjectTeam: the team and whether it is linked to THIS project, in one read.
    [Fact]
    public async Task FindProjectTeam_SaysWhetherTheTeamIsLinkedToTheProject()
    {
        var structure = await database.CreateStructureAsync();
        var (organizationId, projectId) = (structure.Organization.Id, structure.Project.Project.Id);
        var unlinked = Team.Create(organizationId, Name("Не связана"));
        await using (var context = database.NewContext())
        {
            context.Teams.Add(unlinked);
            await context.SaveChangesAsync();
        }

        var linked = await ReadAsync(seam => seam.FindProjectTeamAsync(organizationId, projectId, structure.FirstTeam.Id, CancellationToken.None));
        var notLinked = await ReadAsync(seam => seam.FindProjectTeamAsync(organizationId, projectId, unlinked.Id, CancellationToken.None));

        Assert.True(linked!.IsLinked);
        Assert.False(notLinked!.IsLinked);
        Assert.Equal(TeamStatus.Active, notLinked.Team.Status);
    }

    // feat-028 BR-OL-6 · the projects a team is linked to, reachable ones only, by name.
    [Fact]
    public async Task ListLinkedProjects_ReachableOnly_ByName()
    {
        var structure = await database.CreateStructureAsync();
        var organizationId = structure.Organization.Id;
        var second = await database.AddProjectAsync(organizationId, "Аналитика", "ANL");
        var old = await database.AddProjectAsync(organizationId, "Старый", "OLD");
        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @id", ("id", old.Project.Id));
        await using (var context = database.NewContext())
        {
            context.ProjectTeams.AddRange(
                ProjectTeam.Link(organizationId, second.Project.Id, structure.FirstTeam.Id, Creator, Now),
                ProjectTeam.Link(organizationId, old.Project.Id, structure.FirstTeam.Id, Creator, Now));
            await context.SaveChangesAsync();
        }

        var projects = await ReadAsync(seam => seam.ListLinkedProjectsAsync(organizationId, structure.FirstTeam.Id, CancellationToken.None));

        Assert.Equal(["Аналитика", "Пример проекта"], projects.Select(project => project.Name));
        Assert.All(projects, project => Assert.True(project.IsReachable));
    }

    // US-8 · "the structure contract reads inside the organization": an object of another organization is null; the lists are empty;
    // and every read asked in its own organization answers with its status.
    [Fact]
    public async Task EveryRead_OfAnotherOrganization_IsNullOrEmpty_AskedInItsOwn_AnswersWithItsStatus()
    {
        var structure = await database.CreateStructureAsync();
        var other = await database.CreateStructureAsync();
        var (organizationId, projectId, teamId) = (structure.Organization.Id, structure.Project.Project.Id, structure.FirstTeam.Id);

        Assert.Null(await ReadAsync(seam => seam.FindProjectAsync(other.Organization.Id, projectId, CancellationToken.None)));
        Assert.Null(await ReadAsync(seam => seam.FindTeamAsync(other.Organization.Id, teamId, CancellationToken.None)));
        Assert.Null(await ReadAsync(seam => seam.FindProjectTeamAsync(other.Organization.Id, projectId, teamId, CancellationToken.None)));
        Assert.Empty(await ReadAsync(seam => seam.ListLinkedTeamsAsync(other.Organization.Id, projectId, CancellationToken.None)));
        Assert.Empty(await ReadAsync(seam => seam.ListLinkedProjectsAsync(other.Organization.Id, teamId, CancellationToken.None)));

        Assert.NotNull(await ReadAsync(seam => seam.FindProjectAsync(organizationId, projectId, CancellationToken.None)));
        Assert.NotNull(await ReadAsync(seam => seam.FindTeamAsync(organizationId, teamId, CancellationToken.None)));
        Assert.NotEmpty(await ReadAsync(seam => seam.ListLinkedTeamsAsync(organizationId, projectId, CancellationToken.None)));
        Assert.NotEmpty(await ReadAsync(seam => seam.ListLinkedProjectsAsync(organizationId, teamId, CancellationToken.None)));
    }

    // api-contract § 7: null means only that no such object ever existed.
    [Fact]
    public async Task EveryRead_UnknownId_IsNullOrEmpty()
    {
        var unknown = Guid.CreateVersion7();
        var organizationId = Guid.CreateVersion7();

        Assert.Null(await ReadAsync(seam => seam.FindOrganizationAsync(unknown, CancellationToken.None)));
        Assert.Null(await ReadAsync(seam => seam.FindProjectAsync(organizationId, unknown, CancellationToken.None)));
        Assert.Null(await ReadAsync(seam => seam.FindTeamAsync(organizationId, unknown, CancellationToken.None)));
        Assert.Null(await ReadAsync(seam => seam.FindProjectTeamAsync(organizationId, unknown, unknown, CancellationToken.None)));
        Assert.Empty(await ReadAsync(seam => seam.ListLinkedTeamsAsync(organizationId, unknown, CancellationToken.None)));
        Assert.Empty(await ReadAsync(seam => seam.ListLinkedProjectsAsync(organizationId, unknown, CancellationToken.None)));
    }

    private async Task<TResult> ReadAsync<TResult>(Func<IOrganizationStructure, Task<TResult>> read)
    {
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<IOrganizationStructure>());
    }
}
