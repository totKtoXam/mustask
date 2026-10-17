using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Infrastructure.Tests.Organizations.OrganizationsDatabaseFixture;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// api-contract § 7: the reads M1 offers other modules. By-id reads return the
/// object with its status, reachable or not (ADR-0026 rule 4); <c>null</c>
/// means only that no such object ever existed.
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

    // US-6 · "Other modules get the same answer": the project, its root and its reachability. mod-017 BR-OT-4 (renamed from
    // …ReturnsFirstTeamRootAndReachability, there is no first team): a project with two teams and none named has no default
    // team.
    [Fact]
    public async Task FindProject_ReturnsTheDefaultTeamRootAndReachability()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;

        var reachable = await ReadAsync(seam => seam.FindProjectAsync(projectId, CancellationToken.None));
        await database.ExecuteAsync("UPDATE organizations SET status = 'Deactivated' WHERE id = @id", ("id", structure.Organization.Id));
        var underDeactivated = await ReadAsync(seam => seam.FindProjectAsync(projectId, CancellationToken.None));

        Assert.Equal(
            new ProjectInfo(
                projectId,
                structure.Organization.Id,
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

    // mod-017 BR-OT-4 · the one definition of the default: the named team; the only team when none is named; none when there are
    // several and none is named; and a named team that was deleted is no longer named.
    [Fact]
    public async Task FindProject_DefaultTeam_NamedOnlyTeamNone_AndANamedTeamThatWasDeleted()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        var (first, second) = (structure.Project.FirstTeam.Id, structure.SecondTeam.Team.Id);

        Assert.Null((await ReadAsync(seam => seam.FindProjectAsync(projectId, CancellationToken.None)))!.DefaultTeamId);

        await database.ExecuteAsync("UPDATE projects SET default_team_id = @team WHERE id = @id", ("team", second), ("id", projectId));
        Assert.Equal(second, (await ReadAsync(seam => seam.FindProjectAsync(projectId, CancellationToken.None)))!.DefaultTeamId);

        await database.ExecuteAsync(
            "UPDATE areas SET owner_team_id = @first, default_of_team_id = NULL WHERE owner_team_id = @second; "
            + "UPDATE teams SET status = 'Deleted' WHERE id = @second",
            ("first", first),
            ("second", second));
        Assert.Equal(first, (await ReadAsync(seam => seam.FindProjectAsync(projectId, CancellationToken.None)))!.DefaultTeamId);

        await database.ExecuteAsync("UPDATE projects SET default_team_id = NULL WHERE id = @id", ("id", projectId));
        Assert.Equal(first, (await ReadAsync(seam => seam.FindProjectAsync(projectId, CancellationToken.None)))!.DefaultTeamId);
    }

    // US-7 · "Other modules read the deleted status" (doc-010 PR-9): Deleted, never null and never active, with no default.
    [Fact]
    public async Task FindTeam_DeletedTeam_AnswersDeleted_ListActiveTeamsLeavesItOut()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Project.Id;
        var secondId = structure.SecondTeam.Team.Id;

        var before = await ReadAsync(seam => seam.FindTeamAsync(secondId, CancellationToken.None));
        await database.ExecuteAsync(
            "UPDATE areas SET owner_team_id = @first, default_of_team_id = NULL WHERE owner_team_id = @second; "
            + "UPDATE teams SET status = 'Deleted' WHERE id = @second",
            ("first", structure.Project.FirstTeam.Id),
            ("second", secondId));
        var after = await ReadAsync(seam => seam.FindTeamAsync(secondId, CancellationToken.None));
        var active = await ReadAsync(seam => seam.ListActiveTeamsAsync(projectId, CancellationToken.None));

        Assert.Equal(
            new TeamInfo(secondId, projectId, structure.Organization.Id, "Платформа", TeamStatus.Active, false, structure.SecondTeam.Area.Id, true),
            before);
        Assert.Equal(
            new TeamInfo(secondId, projectId, structure.Organization.Id, "Платформа", TeamStatus.Deleted, false, null, false),
            after);
        Assert.Equal([structure.Project.FirstTeam.Id], active.Select(team => team.Id));
        Assert.True(active[0].IsFirstTeam);
        Assert.Equal(structure.Project.Root.Id, active[0].DefaultAreaId);
    }

    // US-6 · "Other modules get the same answer as the tree": «Платформа / API / Шлюз» resolves to «Платформа», inherited.
    [Fact]
    public async Task FindAreaOwner_ResolvesThroughTheTree()
    {
        var structure = await database.CreateStructureAsync();
        var api = Area.CreateChild(structure.SecondTeam.Area, Name("API"), ownerTeam: null);
        var gateway = Area.CreateChild(api, Name("Шлюз"), ownerTeam: null);
        var reports = Area.CreateChild(structure.Project.Root, Name("Отчёты"), ownerTeam: null);
        await database.AddAreasAsync(api, gateway, reports);

        var gatewayOwner = await ReadAsync(seam => seam.FindAreaOwnerAsync(gateway.Id, CancellationToken.None));
        var platformOwner = await ReadAsync(seam => seam.FindAreaOwnerAsync(structure.SecondTeam.Area.Id, CancellationToken.None));
        var reportsOwner = await ReadAsync(seam => seam.FindAreaOwnerAsync(reports.Id, CancellationToken.None));

        var projectId = structure.Project.Project.Id;
        Assert.Equal(new AreaOwnerInfo(gateway.Id, projectId, structure.SecondTeam.Team.Id, true, true), gatewayOwner);
        Assert.Equal(new AreaOwnerInfo(structure.SecondTeam.Area.Id, projectId, structure.SecondTeam.Team.Id, false, true), platformOwner);
        Assert.Equal(new AreaOwnerInfo(reports.Id, projectId, structure.Project.FirstTeam.Id, true, true), reportsOwner);
    }

    // api-contract § 7: null means only that no such object ever existed.
    [Fact]
    public async Task EveryRead_UnknownId_IsNullOrEmpty()
    {
        var unknown = Guid.CreateVersion7();

        Assert.Null(await ReadAsync(seam => seam.FindOrganizationAsync(unknown, CancellationToken.None)));
        Assert.Null(await ReadAsync(seam => seam.FindProjectAsync(unknown, CancellationToken.None)));
        Assert.Null(await ReadAsync(seam => seam.FindTeamAsync(unknown, CancellationToken.None)));
        Assert.Null(await ReadAsync(seam => seam.FindAreaOwnerAsync(unknown, CancellationToken.None)));
        Assert.Empty(await ReadAsync(seam => seam.ListActiveTeamsAsync(unknown, CancellationToken.None)));
    }

    private async Task<TResult> ReadAsync<TResult>(Func<IOrganizationStructure, Task<TResult>> read)
    {
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<IOrganizationStructure>());
    }
}
