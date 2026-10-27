using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams</c> (feat-028 api-contract § 2.1, § 1.2; US-1, US-7, US-8): the organization's
/// active teams with their active projects, by name, through the real host and a real PostgreSQL. Every test builds its own
/// organization (<see cref="OrganizationsTestData"/>): the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetOrganizationTeamsEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> ListAsync(Guid organizationId, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.OrgTeams(organizationId), token: token ?? OrganizationsTokens.SignedIn());

    private static List<(string Name, string[] Projects)> Read(JsonElement body) =>
        body.EnumerateArray()
            .Select(team => (
                team.GetProperty("name").GetString()!,
                team.GetProperty("projects").EnumerateArray().Select(project => project.GetProperty("name").GetString()!).ToArray()))
            .ToList();

    // feat-028 US-1 · "the organization's teams are listed": by name in Russian collation with letter case not deciding, the
    // organization's active teams only — a deleted team and another organization's team are not in it.
    [Fact]
    public async Task Get_ListsTheOrganizationsActiveTeams_ByNameAnyCase_NoDeletedNoOtherOrganization()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "вега");
        await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Бета");
        await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "альфа");
        var gone = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Ушла");
        await OrganizationsTestData.AddUnlinkedTeamAsync(factory, other.Id, "Чужая");
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteTeamAsync(factory, organization.Id, gone.Id)).Status);

        var answer = await ListAsync(organization.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["альфа", "Бета", "вега"], Read(answer.Body).Select(team => team.Name));
    }

    // feat-028 api-contract § 1.2, feat-040 api-contract § 4.1 · the item is exactly {id, name, description, projects, isMember}; a
    // project in it is {id, name}.
    [Fact]
    public async Task Get_ItemShape_IsIdNameDescriptionProjectsIsMember()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await CreateTeamAsync(factory, structure.OrganizationId, new { name = "Описанная", description = " как набрано ", projectIds = new[] { structure.ProjectId.ToString() } });

        var answer = await ListAsync(structure.OrganizationId);

        var described = answer.Body.EnumerateArray().Single(team => team.GetProperty("name").GetString() == "Описанная");
        Assert.Equal(["description", "id", "isMember", "name", "projects"], PropertyNames(described));
        Assert.Equal(" как набрано ", described.GetProperty("description").GetString());
        var project = Assert.Single(described.GetProperty("projects").EnumerateArray());
        Assert.Equal(["id", "name"], PropertyNames(project));
        Assert.Equal(structure.ProjectId, project.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, answer.Body.EnumerateArray().First(team => team.GetProperty("name").GetString() == "Платформа").GetProperty("description").ValueKind);
    }

    // feat-040 api-contract § 4.1 (BR-TS-10) · `isMember` is the CALLER's: a member of one team sees it true on that team only, a
    // member of none (an «Администратор» who belongs to no team, doc-013) sees every team false, and the list's order (by name)
    // is the same for both — the panel groups the user's own teams first, the server does not.
    [Fact]
    public async Task Get_IsMember_IsTheCallers_ForMemberOfOneTeam_AndForAnAdministratorOfNone()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var venus = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Венера");
        var max = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var ann = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, venus.Id, max.Id);

        var forMax = await ListAsync(structure.OrganizationId, max.Token);
        var forAnn = await ListAsync(structure.OrganizationId, ann.Token);

        Assert.Equal(
            [("Венера", true), ("Платформа", false), ("Пример проекта", false)],
            forMax.Body.EnumerateArray().Select(team => (team.GetProperty("name").GetString()!, team.GetProperty("isMember").GetBoolean())));
        Assert.All(forAnn.Body.EnumerateArray(), team => Assert.False(team.GetProperty("isMember").GetBoolean()));
        Assert.Equal(Ids(forMax.Body), Ids(forAnn.Body));
    }

    // feat-028 BR-OL-1 · a team linked to no project is listed with an empty `projects`; a team linked to several lists them all, by
    // name (Russian collation, letter case not deciding) then id.
    [Fact]
    public async Task Get_ProjectsOfATeam_ByName_NoneIsEmpty()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var zeta = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Зета", "ZET");
        var alpha = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "альфа", "ALP");
        var beta = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Бета", "BET");
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Много проектов");
        await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Ничьих проектов");
        foreach (var project in new[] { zeta, alpha, beta })
        {
            await OrganizationsTestData.LinkAsync(factory, project.Id, team.Id);
        }

        var answer = await ListAsync(organization.Id);

        var read = Read(answer.Body);
        Assert.Equal(["Много проектов", "Ничьих проектов"], read.Select(team => team.Name));
        Assert.Equal(["альфа", "Бета", "Зета"], read[0].Projects);
        Assert.Empty(read[1].Projects);
    }

    // feat-028 api-contract § 1.2 · "a team's active projects only": a deactivated project leaves the item, the link survives, and a
    // restore brings the project back; a team whose only project is deactivated has `[]`.
    [Fact]
    public async Task Get_DeactivatedProjectsAreOmitted_TheLinkSurvivesTheRestore()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        await OrganizationsTestData.LinkAsync(factory, second.Id, structure.Platform.Id);

        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, second.Id, ProjectStatus.Deactivated);
        var whileDeactivated = Read((await ListAsync(structure.OrganizationId)).Body).Single(team => team.Name == "Платформа");
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);
        var bothDeactivated = Read((await ListAsync(structure.OrganizationId)).Body).Single(team => team.Name == "Платформа");
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, second.Id, ProjectStatus.Active);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Active);
        var restored = Read((await ListAsync(structure.OrganizationId)).Body).Single(team => team.Name == "Платформа");

        Assert.Equal(["Пример проекта"], whileDeactivated.Projects);
        Assert.Empty(bothDeactivated.Projects);
        Assert.Equal(["Второй проект", "Пример проекта"], restored.Projects);
        Assert.Equal(2, (await LinksOfTeamAsync(factory, structure.Platform.Id)).Count);
    }

    // feat-028 BR-OL-13 · "no team is the first state after the upgrade": an organization with projects and no team lists `[]`.
    [Fact]
    public async Task Get_OrganizationWithProjectsAndNoTeam_ReturnsEmpty()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);

        var answer = await ListAsync(organization.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        Assert.Empty(answer.Body.EnumerateArray());
    }

    // feat-028 US-8 · "names and lists are the organization's own": the other organization's team never appears, and the project each
    // team lists is its own organization's (a link across organizations does not exist).
    [Fact]
    public async Task Get_NeverListsAnotherOrganizationsTeam()
    {
        var first = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateStructureAsync(factory);

        var firstList = Read((await ListAsync(first.OrganizationId)).Body);
        var secondList = Read((await ListAsync(second.OrganizationId)).Body);

        Assert.Equal(["Платформа", "Пример проекта"], firstList.Select(team => team.Name));
        Assert.Equal(["Платформа", "Пример проекта"], secondList.Select(team => team.Name));
        Assert.All(firstList.Concat(secondList), team => Assert.Equal(["Пример проекта"], team.Projects));
        Assert.Equal(
            (await TeamTestData.StoredTeamsAsync(factory, first.OrganizationId)).Select(team => team.Id).Order(),
            Ids((await ListAsync(first.OrganizationId)).Body).Order());
    }

    // feat-028 US-1 · "roles": every member of the organization reads the list (organizations.team.read).
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Get_MemberViewerAndAdmin_AllRead(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);

        var answer = await ListAsync(structure.OrganizationId, caller.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(2, answer.Body.GetArrayLength());
    }

    // feat-028 US-1 · a person who is not a member of the organization — an instance administrator included, a token carrying every
    // code — gets 404 organization.not_found, never a 403 (ADR-0027).
    [Theory]
    [InlineData("stranger")]
    [InlineData("every code")]
    public async Task Get_NonMemberOrInstanceAdministrator_Returns404Organization(string who)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var token = who == "stranger"
            ? OrganizationsTokens.Stranger()
            : KeycloakTestRealm.CreateToken([.. PermissionCatalog.AllCodes], options => options.Subject = OrganizationsTokens.NewSubject());

        var answer = await ListAsync(structure.OrganizationId, token);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (answer.Status, answer.ErrorCode));
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.OrgTeams(structure.OrganizationId));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    // BR-M1-20 · a deactivated or unknown organization answers its own not-found.
    [Theory]
    [InlineData("unknown")]
    [InlineData("deactivated")]
    public async Task Get_OrganizationNotActive_Returns404OrganizationNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        if (how == "deactivated")
        {
            await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Deactivated);
        }

        var answer = await ListAsync(how == "unknown" ? Guid.CreateVersion7() : structure.OrganizationId);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (answer.Status, answer.ErrorCode));
    }
}
