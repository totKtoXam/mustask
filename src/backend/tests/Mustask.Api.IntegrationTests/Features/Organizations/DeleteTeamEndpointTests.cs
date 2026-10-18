using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/teams/{teamId}</c> (feat-028 api-contract § 2.1, US-5, BR-OL-9, BR-OL-10; mod-017
/// US-7) through the real host and a real PostgreSQL. Every test builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied. Reads and writes of other endpoints (the team list, the team, the rename, the default naming)
/// stand in for "from now on every request answers not found". The deletion's operation over what the team owns, the missing
/// last-team guard and the races are in <see cref="DeleteOwnedItemsEndpointTests"/>. Rewritten from the project-nested route (D-16):
/// the project-addressed "wrong container" rows are retired (BR-OL-12) and the not-found rows are the organization's.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteTeamEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFoundCode = "organizations.team.not_found";

    private Task<Answer> DeleteAsync(Guid organizationId, string teamId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.OrgTeams(organizationId)}/{Uri.EscapeDataString(teamId)}",
            token: token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> DeleteAsync(OrganizationStructure structure, Guid teamId) =>
        DeleteAsync(structure.OrganizationId, teamId.ToString());

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

    private Task<Team> StoredAsync(Guid teamId) => StoredTeamAsync(factory, teamId);

    // US-5 · "A team is deleted ... no picker offers it": «Платформа» is no longer among the project's teams, and the others are still
    // listed.
    [Fact]
    public async Task Delete_TeamLeavesTheProjectsListAndTheOrganizationsList()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var data = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Данные");
        var teamsPath = OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId);

        var listed = await GetAsync(teamsPath);
        var answer = await DeleteAsync(structure, structure.Platform.Id);
        var remaining = await GetAsync(teamsPath);
        var organizationList = await GetAsync(OrganizationsApi.OrgTeams(structure.OrganizationId));

        Assert.Equal(HttpStatusCode.OK, listed.Status);
        Assert.Equal(new[] { structure.ProjectTeamId, data.Id, structure.Platform.Id }.Order(), Ids(listed.Body).Order());
        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(new[] { structure.ProjectTeamId, data.Id }.Order(), Ids(remaining.Body).Order());
        Assert.DoesNotContain(structure.Platform.Id, Ids(organizationList.Body));
        Assert.Empty(await LinksOfTeamAsync(factory, structure.Platform.Id));
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Delete_NoToken_Returns401_DeletesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.OrgTeams(structure.OrganizationId)}/{structure.Platform.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(TeamStatus.Active, (await StoredAsync(structure.Platform.Id)).Status);
    }

    // US-5 · "only an administrator deletes": a member and a viewer get 403 (organizations.team.delete); nothing is deleted.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Delete_MemberAndViewer_Return403_DeleteNothing(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);

        var answer = await DeleteAsync(structure.OrganizationId, structure.Platform.Id.ToString(), caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        Assert.Equal(TeamStatus.Active, (await StoredAsync(structure.Platform.Id)).Status);
    }

    // US-8 · a team that is not an active team of THIS organization — malformed, unknown, of another organization, already deleted —
    // answers team.not_found, and nothing changes, the other organization's team included.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("team of another organization")]
    [InlineData("deleted")]
    public async Task Delete_TeamNotAnActiveTeamOfTheOrganization_Returns404TeamNotFound_ChangesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Старая команда");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Id);
        var teamId = how switch
        {
            "malformed" => structure.Platform.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "team of another organization" => other.Platform.Id.ToString(),
            _ => deleted.Id.ToString(),
        };

        var answer = await DeleteAsync(structure.OrganizationId, teamId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal(TeamNotFoundCode, answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
        Assert.Equal(TeamStatus.Active, (await StoredAsync(structure.Platform.Id)).Status);
        Assert.Equal(TeamStatus.Active, (await StoredAsync(other.Platform.Id)).Status);
        Assert.Equal(2, (await LinksOfProjectAsync(factory, other.ProjectId)).Count);
    }

    // US-5 · "A deleted team answers not found" (mod-017 BR-OT-12): «Платформа» is deleted, then its address is opened, a request
    // renames it, describes it, names it the project's default team, deletes it again, counts what it owns, lists its projects,
    // reads its backlog or its board, or binds an item to it by a save or a creation. Every answer is team.not_found, and nothing
    // changes: the name stays.
    [Theory]
    [InlineData("read")]
    [InlineData("rename")]
    [InlineData("describe")]
    [InlineData("default")]
    [InlineData("delete")]
    [InlineData("deletion-counts")]
    [InlineData("unlink-count")]
    [InlineData("projects")]
    [InlineData("backlog")]
    [InlineData("board")]
    [InlineData("save")]
    [InlineData("create")]
    public async Task Delete_ThenEveryTeamOperationAndOwnerBinding_Returns404TeamNotFound(string operation)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Id;
        var items = new WorkItemsWorld(
            structure.OrganizationId,
            structure.ProjectId,
            structure.RootId,
            "PRM",
            await WorkItemsTestData.TypesAsync(factory, structure.OrganizationId));
        var item = await WorkItemsTestData.CreateItemAsync(factory, items, "TASK", "Задача");
        var teamPath = Team(structure.OrganizationId, teamId);
        var projectTeamPath = ProjectTeam(structure.OrganizationId, structure.ProjectId, teamId);
        var deletion = await DeleteAsync(structure, teamId);
        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);

        var answer = operation switch
        {
            "read" => await GetAsync(teamPath),
            "rename" => await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{teamPath}/name", new { name = "Новое имя" }, OrganizationsTokens.SignedIn()),
            "describe" => await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{teamPath}/description", new { description = "Текст" }, OrganizationsTokens.SignedIn()),
            "default" => await OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Put,
                $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/default-team",
                new { teamId = teamId.ToString() },
                OrganizationsTokens.SignedIn()),
            "delete" => await DeleteAsync(structure, teamId),
            "deletion-counts" => await GetAsync($"{teamPath}/deletion-counts"),
            "unlink-count" => await GetAsync($"{projectTeamPath}/unlink-count"),
            "projects" => await GetAsync($"{teamPath}/projects"),
            "backlog" => await GetAsync($"{projectTeamPath}/backlogs/stories"),
            "board" => await GetAsync($"{projectTeamPath}/boards/stories"),
            "save" => await OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Patch,
                WorkItemsTestData.Item(items, item),
                new { team = new { teamId = teamId.ToString() } },
                OrganizationsTokens.SignedIn()),
            _ => await OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Post,
                WorkItemsTestData.List(items),
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

        var stored = await StoredAsync(teamId);
        Assert.Equal("Платформа", stored.Name);
        Assert.Equal(TeamStatus.Deleted, stored.Status);
    }

    // US-5 · "Other modules read the deleted status": the linked teams leave «Платформа» out, and asking for it by its identity says it
    // is deleted, never active and never missing (feat-028 api-contract § 6).
    [Fact]
    public async Task Delete_Seam_FindTeamSaysDeleted_ListLinkedTeamsOmitsIt()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var data = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Данные");
        var platform = structure.Platform;

        var answer = await DeleteAsync(structure, platform.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);

        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();

        var deleted = await seam.FindTeamAsync(structure.OrganizationId, platform.Id, CancellationToken.None);
        Assert.NotNull(deleted);
        Assert.Equal(TeamStatus.Deleted, deleted.Status);
        Assert.False(deleted.IsReachable);
        Assert.Equal(structure.OrganizationId, deleted.OrganizationId);

        var linked = await seam.ListLinkedTeamsAsync(structure.OrganizationId, structure.ProjectId, CancellationToken.None);
        Assert.Equal(new[] { structure.ProjectTeamId, data.Id }.Order(), linked.Select(team => team.Id).Order());
        Assert.All(linked, team => Assert.Equal(TeamStatus.Active, team.Status));
    }

    // mod-016 US-3 · "A deleted team's memberships end" (BR-M1-18, BR-M1-36): «Платформа» has two members, one of whom is also in the
    // first team. After the deletion the deleted team has no members; the first team keeps hers; both stay members of the
    // organization; and the deleted team's member list and page answer `team.not_found`.
    [Fact]
    public async Task Delete_EndsTheTeamsMemberships_TheOtherTeamKeepsHers_BothStayInTheOrganization()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, creatorJoinsFirstTeam: false);
        var teamId = structure.Platform.Id;
        var reader = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var both = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var onlyHere = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, both.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, onlyHere.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.ProjectTeamId, both.Id);

        var answer = await DeleteAsync(structure, teamId);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Empty(await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
        Assert.Equal([both.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.ProjectTeamId));
        Assert.NotNull(await MembershipTestData.FindMembershipAsync(factory, structure.OrganizationId, both.Id));
        Assert.NotNull(await MembershipTestData.FindMembershipAsync(factory, structure.OrganizationId, onlyHere.Id));

        var teamPath = Team(structure.OrganizationId, teamId);
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
        var other = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.ProjectTeamId, other.Id);

        var answer = await DeleteAsync(structure, structure.Platform.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal([other.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.ProjectTeamId));
    }

    // US-5 · "no guard code": the retired guard's code is never answered; a deletion that leaves a project with no team is a 204.
    [Fact]
    public async Task Delete_NoGuardCode_IsAnswered()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id);

        var answer = await DeleteAsync(organization.Id, project.Team.Id.ToString());

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(answer.ErrorCode);
    }
}
