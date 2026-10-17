using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams</c>
/// (mod-017 api-contract § 1.3) through the real host and a real PostgreSQL (plan-backend
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

    // mod-017 US-4 · "The project's teams": every active team is listed by name — Russian collation, letter case not deciding,
    // «Ё» with «Е» — and no team is first (BR-OT-11), although the team the project came with sorts last here; the one the
    // settings name carries «named» (BR-OT-4). The teams are created out of order, so neither the order of creation nor the order
    // of ids explains the answer. Replaces Get_ActiveTeams_FirstTeamFirstThenDisplayOrder_WithDefaultAreaPaths (D-16).
    [Fact]
    public async Task Get_ActiveTeams_ByName_NoTeamFirst_NamedTeamMarked()
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

        await OrganizationsTestData.SetDefaultTeamAsync(factory, organization.Id, projectId, platform.Id);

        var answer = await GetAsync(organization.Id, projectId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        Assert.All(answer.Body.EnumerateArray(), item =>
        {
            // mod-016 api-contract § 3.3: each team says whether the caller is in it. mod-017 § 1.3: «defaultMark» replaces
            // «isFirstTeam» and «defaultArea».
            Assert.Equal(["defaultMark", "id", "isMember", "name", "projectId"], PropertyNames(item));
        });
        Assert.Equal(
            [
                new ListedTeam(alpha.Id, projectId, "альфа", null),
                new ListedTeam(beta.Id, projectId, "Бета", null),
                new ListedTeam(fir.Id, projectId, "Ёлка", null),
                new ListedTeam(spruce.Id, projectId, "Ель", null),
                new ListedTeam(platform.Id, projectId, "Платформа", "named"),
                new ListedTeam(project.Team.Id, projectId, "Пример проекта", null),
            ],
            Read(answer.Body));
    }

    // mod-017 BR-OT-4 · "no team is named": with several teams none carries a mark; the mark is the only difference between
    // the reads before and after the setting is cleared.
    [Fact]
    public async Task Get_DefaultMark_NamedOnlyTeamNone()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id);
        var projectId = project.Project.Id;

        var only = Read((await GetAsync(organization.Id, projectId)).Body);
        var platform = await OrganizationsTestData.AddTeamAsync(factory, projectId, "Платформа");
        var several = Read((await GetAsync(organization.Id, projectId)).Body);
        await OrganizationsTestData.SetDefaultTeamAsync(factory, organization.Id, projectId, project.Team.Id);
        var named = Read((await GetAsync(organization.Id, projectId)).Body);
        await OrganizationsTestData.SetDefaultTeamAsync(factory, organization.Id, projectId, null);
        await OrganizationsTestData.DeleteTeamAsync(factory, projectId, project.Team.Id);
        var onlyAgain = Read((await GetAsync(organization.Id, projectId)).Body);

        Assert.Equal([("Пример проекта", "onlyTeam")], only.Select(team => (team.Name, team.DefaultMark)));
        Assert.Equal([("Платформа", (string?)null), ("Пример проекта", null)], several.Select(team => (team.Name, team.DefaultMark)));
        Assert.Equal([("Платформа", (string?)null), ("Пример проекта", "named")], named.Select(team => (team.Name, team.DefaultMark)));
        Assert.Equal([(platform.Id, "onlyTeam")], onlyAgain.Select(team => (team.Id, team.DefaultMark)));
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

        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, platform.Id, me);
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, analytics.Id, me);
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, project.Team.Id, you);

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
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, platform.Id, me);
        var added = await IsMemberByNameAsync(organization.Id, project.Project.Id, me);
        await MembershipTestData.RemoveMemberAsync(factory, organization.Id, me);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, me);
        var afterLeaving = await IsMemberByNameAsync(organization.Id, project.Project.Id, me);

        Assert.False(before["Платформа"]);
        Assert.True(added["Платформа"]);
        Assert.False(afterLeaving["Платформа"]);
    }

    // mod-017 US-3 · "A project is created ready to use": its one team, named after it, is the project's only team, so it carries
    // «onlyTeam» (BR-OT-4); the list has no area to show. Replaces Get_NewProject_ListsTheFirstTeamAlone_DefaultAreaIsTheRoot (D-16).
    [Fact]
    public async Task Get_NewProject_ListsItsOnlyTeamAlone_MarkedOnlyTeam()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Аналитика", "ANL");

        var answer = await GetAsync(organization.Id, project.Project.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            [new ListedTeam(project.Team.Id, project.Project.Id, "Аналитика", "onlyTeam")],
            Read(answer.Body));
    }

    // US-8 · "The switcher lists ... «Платформа» and «Пример проекта», and not the deleted team" (BR-M1-18).
    [Fact]
    public async Task Get_DeletedTeamLeftOut()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var core = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, core.Id);

        var answer = await GetAsync(structure.OrganizationId, structure.ProjectId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            [
                new ListedTeam(structure.Platform.Id, structure.ProjectId, "Платформа", null),
                new ListedTeam(structure.ProjectTeamId, structure.ProjectId, "Пример проекта", null),
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

    // NFR Volumes (QA pass 1, A-18): "tens of teams per project" return within 2 s, each with its default mark. Thirty teams
    // beside the first two; one warm-up request first, so the bound measures the read, not the first call's JIT. Replaces
    // Get_ThirtyTwoTeams_ReturnsWithinTwoSeconds_FirstTeamFirst (D-16, BR-OT-11).
    [Fact]
    public async Task Get_ThirtyTwoTeams_ReturnsWithinTwoSeconds_ByName()
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
        Assert.Equal(
            Enumerable.Range(0, 30).Select(index => $"Команда {index:D2}").Append("Платформа").Append("Пример проекта"),
            teams.Select(team => team.Name));
        Assert.All(teams, team => Assert.Null(team.DefaultMark));
    }

    private static IReadOnlyList<ListedTeam> Read(JsonElement body) =>
        body.EnumerateArray()
            .Select(item => new ListedTeam(
                item.GetProperty("id").GetGuid(),
                item.GetProperty("projectId").GetGuid(),
                item.GetProperty("name").GetString()!,
                item.GetProperty("defaultMark").GetString()))
            .ToList();

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    /// <summary>One element of the list, flattened for comparison.</summary>
    private sealed record ListedTeam(Guid Id, Guid ProjectId, string Name, string? DefaultMark);
}
