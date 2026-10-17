using System.Net;
using System.Text.Json;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}</c>
/// (mod-017 api-contract § 2.7, US-7, BR-OT-11, BR-OT-12) through the real host and a real PostgreSQL. Every
/// test builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied. Reads and writes of other lanes' endpoints (the team
/// list, the team, the rename, the default naming) stand in for
/// "from now on every request answers not found". The deletion's operation over what the team owns, the last-team guard and the
/// races of BR-OT-13 are in <see cref="DeleteOwnedItemsEndpointTests"/>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteTeamEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFoundCode = "organizations.team.not_found";

    private Task<Answer> DeleteAsync(
        Guid organizationId, string projectId, string teamId, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Organization(organizationId)}/projects/{Uri.EscapeDataString(projectId)}/teams/{Uri.EscapeDataString(teamId)}",
            token: token ?? OrganizationsTokens.SignedIn(),
            gate: gate);

    private Task<Answer> DeleteAsync(OrganizationStructure structure, Guid teamId, CommitGate? gate = null) =>
        DeleteAsync(structure.OrganizationId, structure.ProjectId.ToString(), teamId.ToString(), gate: gate);

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

    private async Task<TeamInfo> StoredTeamAsync(Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var team = await scope.ServiceProvider.GetRequiredService<IOrganizationStructure>().FindTeamAsync(teamId, CancellationToken.None);

        return team ?? throw new InvalidOperationException($"The team {teamId} was never stored.");
    }

    private static Dictionary<Guid, (Guid? ParentId, string Name, string Path)> Places(AreaTree tree) =>
        tree.PreOrder().ToDictionary(node => node.Id, node => (node.ParentId, node.Name, tree.PathOf(node.Id)));

    // US-7 · "A team is deleted ... no picker offers it": «Платформа» is no longer among the project's teams, and the others
    // are still listed.
    [Fact]
    public async Task Delete_TeamLeavesTheList()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var data = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Данные");
        var teamsPath = OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId);

        var listed = await GetAsync(teamsPath);
        var answer = await DeleteAsync(structure, structure.Platform.Id);
        var remaining = await GetAsync(teamsPath);

        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Equal(
            new[] { structure.ProjectTeamId, data.Id, structure.Platform.Id }.Order(),
            listed.Body.EnumerateArray().Select(team => team.GetProperty("id").GetGuid()).Order());
        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(HttpStatusCode.OK, remaining.Status);
        Assert.Equal(
            new[] { structure.ProjectTeamId, data.Id }.Order(),
            remaining.Body.EnumerateArray().Select(team => team.GetProperty("id").GetGuid()).Order());
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Delete_NoToken_Returns401_DeletesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{structure.Platform.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Id)).Status);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers, and no team is deleted, the other organization's included.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Delete_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var (projectId, teamId) = how switch
        {
            "malformed" => ("PRM", structure.Platform.Id),
            "unknown" => (Guid.CreateVersion7().ToString(), structure.Platform.Id),
            _ => (other.ProjectId.ToString(), other.Platform.Id),
        };

        var answer = await DeleteAsync(structure.OrganizationId, projectId, teamId.ToString());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(other.Platform.Id)).Status);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Delete_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await DeleteAsync(structure, structure.Platform.Id);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Id)).Status);
    }

    // US-4 · "A team id names no team of the project" (BR-M1-22) and US-7 · "A deleted team answers not found": malformed,
    // unknown, a team of another project (the project's own team included: the address answers before the last-team rule,
    // api-contract § 0.4), a team of another organization, or one already deleted. Nothing changes.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("project team of another project")]
    [InlineData("team of another project")]
    [InlineData("team of another organization")]
    [InlineData("deleted")]
    public async Task Delete_TeamNotAnActiveTeamOfTheProject_Returns404TeamNotFound_ChangesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var secondTeam = await OrganizationsTestData.AddTeamAsync(factory, secondProject.Project.Id, "Ядро");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Старая команда");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Id);
        var teamId = how switch
        {
            "malformed" => structure.Platform.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "project team of another project" => secondProject.Team.Id.ToString(),
            "team of another project" => secondTeam.Id.ToString(),
            "team of another organization" => other.Platform.Id.ToString(),
            _ => deleted.Id.ToString(),
        };
        var before = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        var answer = await DeleteAsync(structure.OrganizationId, structure.ProjectId.ToString(), teamId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal(TeamNotFoundCode, answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(structure.Platform.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(secondProject.Team.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(secondTeam.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredTeamAsync(other.Platform.Id)).Status);
        Assert.Equal(Places(before), Places(await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)));
        Assert.Equal(before.PreOrder(), (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).PreOrder());
    }

    // US-7 · "A deleted team answers not found" (mod-017 BR-OT-12): «Платформа» is deleted, then its address is opened, a request
    // renames it, names it the project's default team, deletes it again, counts what it owns, reads its backlog or its board,
    // or binds an item to it by a save or a creation. Every answer is team.not_found, and nothing changes: the name stays.
    [Theory]
    [InlineData("read")]
    [InlineData("rename")]
    [InlineData("default")]
    [InlineData("delete")]
    [InlineData("count")]
    [InlineData("backlog")]
    [InlineData("board")]
    [InlineData("save")]
    [InlineData("create")]
    public async Task Delete_ThenEveryTeamOperationAndOwnerBinding_Returns404TeamNotFound(string operation)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;
        var items = new Mustask.Api.IntegrationTests.Features.WorkItems.WorkItemsWorld(
            structure.OrganizationId,
            structure.ProjectId,
            structure.RootId,
            "PRM",
            await Mustask.Api.IntegrationTests.Features.WorkItems.WorkItemsTestData.TypesAsync(factory, structure.OrganizationId));
        var item = await Mustask.Api.IntegrationTests.Features.WorkItems.WorkItemsTestData.CreateItemAsync(factory, items, "TASK", "Задача");
        var teamPath = $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{teamId}";
        var deletion = await DeleteAsync(structure, teamId);
        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);

        var answer = operation switch
        {
            "read" => await GetAsync(teamPath),
            "rename" => await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{teamPath}/name", new { name = "Новое имя" }, OrganizationsTokens.SignedIn()),
            "default" => await OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Put,
                $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/default-team",
                new { teamId = teamId.ToString() },
                OrganizationsTokens.SignedIn()),
            "delete" => await DeleteAsync(structure, teamId),
            "count" => await GetAsync($"{teamPath}/owned-items/count"),
            "backlog" => await GetAsync($"{teamPath}/backlogs/stories"),
            "board" => await GetAsync($"{teamPath}/boards/stories"),
            "save" => await OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Patch,
                Mustask.Api.IntegrationTests.Features.WorkItems.WorkItemsTestData.Item(items, item),
                new { team = new { teamId = teamId.ToString() } },
                OrganizationsTokens.SignedIn()),
            _ => await OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Post,
                Mustask.Api.IntegrationTests.Features.WorkItems.WorkItemsTestData.List(items),
                new
                {
                    requestId = Guid.NewGuid().ToString(),
                    typeId = items.Types["TASK"].Id.ToString(),
                    title = "Новая",
                    team = new { teamId = teamId.ToString() },
                },
                OrganizationsTokens.SignedIn()),
        };

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal(TeamNotFoundCode, answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);

        var stored = await StoredTeamAsync(teamId);
        Assert.Equal("Платформа", stored.Name);
        Assert.Equal(TeamStatus.Deleted, stored.Status);
    }

    // US-7 · "Other modules read the deleted status": the list of active teams leaves «Платформа» out, and asking for it by its
    // identity says it is deleted, never active and never missing.
    [Fact]
    public async Task Delete_Seam_FindTeamSaysDeleted_ListActiveTeamsOmitsIt()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var data = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Данные");
        var platform = structure.Platform;

        var answer = await DeleteAsync(structure, platform.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);

        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();

        var deleted = await seam.FindTeamAsync(platform.Id, CancellationToken.None);
        Assert.NotNull(deleted);
        Assert.Equal(TeamStatus.Deleted, deleted.Status);
        Assert.False(deleted.IsReachable);
        Assert.Equal(structure.ProjectId, deleted.ProjectId);
        Assert.Equal(structure.OrganizationId, deleted.OrganizationId);

        var active = await seam.ListActiveTeamsAsync(structure.ProjectId, CancellationToken.None);
        Assert.Equal(
            new[] { structure.ProjectTeamId, data.Id }.Order(),
            active.Select(team => team.Id).Order());
        Assert.All(active, team => Assert.Equal(TeamStatus.Active, team.Status));
    }

    // mod-016 US-3 · "A deleted team's memberships end" (mod-002 BR-M1-18, BR-M1-36): «Платформа» has two members, one of whom is
    // also in the first team. After the deletion the deleted team has no members; the first team keeps hers; both stay members of
    // the organization; and the deleted team's member list and page answer `team.not_found`.
    [Fact]
    public async Task Delete_EndsTheTeamsMemberships_TheOtherTeamKeepsHers_BothStayInTheOrganization()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, creatorJoinsFirstTeam: false);
        var teamId = structure.Platform.Id;
        var reader = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Viewer);
        var both = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        var onlyHere = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, both.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, onlyHere.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.ProjectTeamId, both.Id);

        var answer = await DeleteAsync(structure, teamId);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Empty(await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
        Assert.Equal([both.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.ProjectTeamId));
        Assert.NotNull(await MembershipTestData.FindMembershipAsync(factory, structure.OrganizationId, both.Id));
        Assert.NotNull(await MembershipTestData.FindMembershipAsync(factory, structure.OrganizationId, onlyHere.Id));

        var teamPath = $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{teamId}";
        var members = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teamPath}/members", token: reader.Token);
        var page = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, teamPath, token: reader.Token);
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFoundCode), (members.Status, members.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFoundCode), (page.Status, page.ErrorCode));
    }

    // mod-016 US-3: a team with no members is deleted as before — the memberships step finds nothing to end.
    [Fact]
    public async Task Delete_ATeamWithNoMembers_StillSucceeds()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, creatorJoinsFirstTeam: false);
        var other = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.ProjectTeamId, other.Id);

        var answer = await DeleteAsync(structure, structure.Platform.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal([other.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.ProjectTeamId));
    }
}
