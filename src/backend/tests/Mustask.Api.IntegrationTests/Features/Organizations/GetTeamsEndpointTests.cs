using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams</c>
/// (api-contract § 3.1) through the real host and a real PostgreSQL (plan-backend
/// § 14.3, lane L-15). Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied. The requests carry a token with no right at all: the read needs
/// <c>organizations.team.read</c>, which comes from the caller's membership
/// (mod-016 ADR-0027), and the caller is the organization's creator, an administrator.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetTeamsEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> GetAsync(Guid organizationId, string projectId) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/teams",
            token: OrganizationsTokens.SignedIn());

    private Task<Answer> GetAsync(Guid organizationId, Guid projectId) => GetAsync(organizationId, projectId.ToString());

    // US-4 · "The project's teams": every active team is listed with its default area; US-8 · "The switcher lists the active
    // organizations, projects and teams" and "The default team is the project's first team": «Пример проекта» first, then
    // the rest in display order (Russian, ignoring case, «Ё» with «Е»), although «Платформа» sorts before «Пример проекта».
    // The teams are created out of order, so neither the order of creation nor the order of ids explains the answer.
    [Fact]
    public async Task Get_ActiveTeams_FirstTeamFirstThenDisplayOrder_WithDefaultAreaPaths()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id);
        var projectId = project.Project.Id;
        var platform = await OrganizationsTestData.AddTeamAsync(factory, projectId, "Платформа");
        var beta = await OrganizationsTestData.AddTeamAsync(factory, projectId, "Бета");
        var spruce = await OrganizationsTestData.AddTeamAsync(factory, projectId, "Ель");
        var alpha = await OrganizationsTestData.AddTeamAsync(factory, projectId, "альфа");
        var fir = await OrganizationsTestData.AddTeamAsync(factory, projectId, "Ёлка");

        // The teams of another project of the organization are not listed.
        var elsewhere = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Аналитика", "ANL");
        await OrganizationsTestData.AddTeamAsync(factory, elsewhere.Project.Id, "Ядро");

        // US-4 · «Платформа» chooses «Платформа / API», an area it owns explicitly, as its default area.
        var api = await OrganizationsTestData.AddAreaAsync(factory, projectId, platform.Area.Id, "API", platform.Team.Id);
        await OrganizationsTestData.SetTeamDefaultAreaAsync(factory, projectId, platform.Team.Id, api.Id);

        var answer = await GetAsync(organization.Id, projectId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        Assert.All(answer.Body.EnumerateArray(), item =>
        {
            // mod-016 api-contract § 3.3: each team says whether the caller is in it.
            Assert.Equal(["defaultArea", "id", "isFirstTeam", "isMember", "name", "projectId"], PropertyNames(item));
            Assert.Equal(["id", "path"], PropertyNames(item.GetProperty("defaultArea")));
        });
        Assert.Equal(
            [
                new ListedTeam(project.FirstTeam.Id, projectId, "Пример проекта", true, project.Root.Id, "Пример проекта"),
                new ListedTeam(alpha.Team.Id, projectId, "альфа", false, alpha.Area.Id, "альфа"),
                new ListedTeam(beta.Team.Id, projectId, "Бета", false, beta.Area.Id, "Бета"),
                new ListedTeam(fir.Team.Id, projectId, "Ёлка", false, fir.Area.Id, "Ёлка"),
                new ListedTeam(spruce.Team.Id, projectId, "Ель", false, spruce.Area.Id, "Ель"),
                new ListedTeam(platform.Team.Id, projectId, "Платформа", false, api.Id, "Платформа / API"),
            ],
            Read(answer.Body));
    }

    private async Task<Dictionary<string, bool>> IsMemberByNameAsync(Guid organizationId, Guid projectId, string subject)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"/api/organizations/{organizationId}/projects/{projectId}/teams",
            token: OrganizationsTokens.SignedIn(subject));

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.EnumerateArray().ToDictionary(team => team.GetProperty("name").GetString()!, team => team.GetProperty("isMember").GetBoolean());
    }

    // mod-016 US-7 · "Choosing a project selects the user's own team" (BR-M1-27, D-5): each team says whether the CALLER is in it;
    // another user's membership does not count; "in no team of the project" marks none. The panel picks the first marked team.
    [Fact]
    public async Task Get_EachTeamSaysWhetherTheCallerIsInIt_OthersMembershipsDoNotCount_NoTeamMarksNone()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id);
        var projectId = project.Project.Id;
        var platform = await OrganizationsTestData.AddTeamAsync(factory, projectId, "Платформа");
        var analytics = await OrganizationsTestData.AddTeamAsync(factory, projectId, "Аналитика");
        var me = MembershipTestData.NewUserId();
        var you = MembershipTestData.NewUserId();
        var alone = MembershipTestData.NewUserId();
        foreach (var user in new[] { me, you, alone })
        {
            await MembershipTestData.AddMemberAsync(factory, organization.Id, user);
        }

        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, platform.Team.Id, me);
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, analytics.Team.Id, me);
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, project.FirstTeam.Id, you);

        var mine = await IsMemberByNameAsync(organization.Id, projectId, me);
        var yours = await IsMemberByNameAsync(organization.Id, projectId, you);
        var none = await IsMemberByNameAsync(organization.Id, projectId, alone);

        Assert.Equal(new Dictionary<string, bool> { ["Пример проекта"] = false, ["Аналитика"] = true, ["Платформа"] = true }, mine);
        Assert.Equal(new Dictionary<string, bool> { ["Пример проекта"] = true, ["Аналитика"] = false, ["Платформа"] = false }, yours);
        Assert.All(none.Values, flag => Assert.False(flag));
    }

    // mod-016 BR-M1-27 · "a change to them counts from the next time that list is read": the flag follows the add and the removal of
    // a team membership, and the removal of the organization membership (which ends every team membership, BR-M1-36).
    [Fact]
    public async Task Get_TheFlagFollowsTheNextReadAfterAnAddARemovalAndALeavingOfTheOrganization()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id);
        var platform = await OrganizationsTestData.AddTeamAsync(factory, project.Project.Id, "Платформа");
        var me = MembershipTestData.NewUserId();
        await MembershipTestData.AddMemberAsync(factory, organization.Id, me);

        var before = await IsMemberByNameAsync(organization.Id, project.Project.Id, me);
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, platform.Team.Id, me);
        var added = await IsMemberByNameAsync(organization.Id, project.Project.Id, me);
        await MembershipTestData.RemoveMemberAsync(factory, organization.Id, me);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, me);
        var afterLeaving = await IsMemberByNameAsync(organization.Id, project.Project.Id, me);

        Assert.False(before["Платформа"]);
        Assert.True(added["Платформа"]);
        Assert.False(afterLeaving["Платформа"]);
    }

    // US-3 · "A project is created ready to use": its first team, named after it, owns the root and has it as its default
    // area, whose path is the root's own name (DC-9). A new project lists that team alone.
    [Fact]
    public async Task Get_NewProject_ListsTheFirstTeamAlone_DefaultAreaIsTheRoot()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Аналитика", "ANL");

        var answer = await GetAsync(organization.Id, project.Project.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            [new ListedTeam(project.FirstTeam.Id, project.Project.Id, "Аналитика", true, project.Root.Id, "Аналитика")],
            Read(answer.Body));
    }

    // US-8 · "The switcher lists ... «Пример проекта» first, then «Платформа», and not the deleted team" (BR-M1-18): the
    // deletion hands the team's explicit areas to the first team, and the others' default areas are untouched.
    [Fact]
    public async Task Get_DeletedTeamLeftOut()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var core = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, core.Area.Id, "Хранилище", core.Team.Id);
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, core.Team.Id);

        var answer = await GetAsync(structure.OrganizationId, structure.ProjectId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            [
                new ListedTeam(structure.FirstTeamId, structure.ProjectId, "Пример проекта", true, structure.RootId, "Пример проекта"),
                new ListedTeam(structure.Platform.Team.Id, structure.ProjectId, "Платформа", false, structure.Platform.Area.Id, "Платформа"),
            ],
            Read(answer.Body));
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Get_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await GetAsync(structure.OrganizationId, projectId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
    }

    // US-3 · "A project is deactivated": everything under it answers project.not_found (ADR-0026 rule 5), the teams too.
    [Fact]
    public async Task Get_UnderDeactivatedProject_Returns404ProjectNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await GetAsync(structure.OrganizationId, structure.ProjectId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
    }

    // NFR Volumes (QA pass 1, A-18): "tens of teams per project" return within 2 s, each with its default area. Thirty
    // teams beside the first two; one warm-up request first, so the bound measures the read, not the first call's JIT.
    [Fact]
    public async Task Get_ThirtyTwoTeams_ReturnsWithinTwoSeconds_FirstTeamFirst()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        for (var index = 0; index < 30; index++)
        {
            await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, $"Команда {index:D2}");
        }

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(structure.OrganizationId, structure.ProjectId)).Status);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var answer = await GetAsync(structure.OrganizationId, structure.ProjectId);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The 32-team list took {stopwatch.Elapsed}.");

        var teams = Read(answer.Body);
        Assert.Equal(32, teams.Count);
        Assert.Equal(structure.FirstTeamId, teams[0].Id);
        Assert.All(teams.Skip(1), team => Assert.Equal(team.Name, team.DefaultAreaPath));
    }

    private static IReadOnlyList<ListedTeam> Read(JsonElement body) =>
        body.EnumerateArray()
            .Select(item => new ListedTeam(
                item.GetProperty("id").GetGuid(),
                item.GetProperty("projectId").GetGuid(),
                item.GetProperty("name").GetString()!,
                item.GetProperty("isFirstTeam").GetBoolean(),
                item.GetProperty("defaultArea").GetProperty("id").GetGuid(),
                item.GetProperty("defaultArea").GetProperty("path").GetString()!))
            .ToList();

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    /// <summary>One element of the list, flattened for comparison.</summary>
    private sealed record ListedTeam(Guid Id, Guid ProjectId, string Name, bool IsFirstTeam, Guid DefaultAreaId, string DefaultAreaPath);
}
