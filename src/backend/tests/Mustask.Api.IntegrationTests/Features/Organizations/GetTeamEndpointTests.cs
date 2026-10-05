using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}</c>
/// (api-contract § 3.2) through the real host and a real PostgreSQL. Every test
/// builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetTeamEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> GetAsync(string organizationId, string projectId, string teamId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/projects/{Uri.EscapeDataString(projectId)}/teams/{Uri.EscapeDataString(teamId)}",
            token: token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> GetAsync(OrganizationStructure structure, Guid teamId) =>
        GetAsync(structure.OrganizationId.ToString(), structure.ProjectId.ToString(), teamId.ToString());

    // US-4 · "The first team's default is the root, for good" and "The project's teams": «Пример проекта» is marked as the team
    // that owns the root, and its default area is the root, whose path is its own name (DC-9).
    [Fact]
    public async Task Get_FirstTeam_Returns200_DefaultAreaIsTheRoot()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await GetAsync(structure, structure.FirstTeamId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["defaultArea", "id", "isFirstTeam", "name", "projectId"], PropertyNames(answer.Body));
        Assert.Equal(structure.FirstTeamId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal("Пример проекта", answer.Body.GetProperty("name").GetString());
        Assert.True(answer.Body.GetProperty("isFirstTeam").GetBoolean());

        var defaultArea = answer.Body.GetProperty("defaultArea");
        Assert.Equal(["id", "path"], PropertyNames(defaultArea));
        Assert.Equal(structure.RootId, defaultArea.GetProperty("id").GetGuid());
        Assert.Equal("Пример проекта", defaultArea.GetProperty("path").GetString());
    }

    // US-4 · "A second team comes with its own area": «Платформа» is not the first team, and its default is its own child of
    // the root, so the path is the names below the root.
    [Fact]
    public async Task Get_SecondTeam_Returns200_DefaultAreaIsItsOwnAreaUnderTheRoot()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await GetAsync(structure, structure.Platform.Team.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(structure.Platform.Team.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal("Платформа", answer.Body.GetProperty("name").GetString());
        Assert.False(answer.Body.GetProperty("isFirstTeam").GetBoolean());

        var defaultArea = answer.Body.GetProperty("defaultArea");
        Assert.Equal(structure.Platform.Area.Id, defaultArea.GetProperty("id").GetGuid());
        Assert.Equal("Платформа", defaultArea.GetProperty("path").GetString());
    }

    // US-4 · "A team's default area is chosen among the areas it owns explicitly": with «Отчёты / API» as the default, the
    // read shows that area and its path below the root, which the real read models and AreaTree agree on.
    [Fact]
    public async Task Get_DefaultAreaBelowTheRoot_ReturnsItsPathBelowTheRoot()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var api = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "API", structure.Platform.Team.Id);
        await OrganizationsTestData.SetTeamDefaultAreaAsync(factory, structure.ProjectId, structure.Platform.Team.Id, api.Id);

        var answer = await GetAsync(structure, structure.Platform.Team.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var defaultArea = answer.Body.GetProperty("defaultArea");
        Assert.Equal(api.Id, defaultArea.GetProperty("id").GetGuid());
        Assert.Equal("Отчёты / API", defaultArea.GetProperty("path").GetString());
        Assert.Equal("Платформа", answer.Body.GetProperty("name").GetString());
    }

    // api-contract § 0.2: the canonical form is accepted in either letter case, in every level of the address.
    [Fact]
    public async Task Get_IdsInUpperCase_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await GetAsync(
            structure.OrganizationId.ToString().ToUpperInvariant(),
            structure.ProjectId.ToString().ToUpperInvariant(),
            structure.Platform.Team.Id.ToString().ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(structure.Platform.Team.Id, answer.Body.GetProperty("id").GetGuid());
    }

    // US-1 · "A visitor who is not signed in sees nothing": 401 before any not-found, even for an address that names nothing.
    [Fact]
    public async Task Get_NoToken_Returns401_EvenForAnAddressThatNamesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var known = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{structure.Platform.Team.Id}");
        var unknown = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            "/api/organizations/not-an-id/projects/not-a-project/teams/not-a-team");

        Assert.Equal(HttpStatusCode.Unauthorized, known.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Status);
    }

    // US-2 · "Everything beneath a deactivated organization answers not found" (BR-M1-20): the organization level answers
    // first, whatever the project and the team are.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deactivated")]
    public async Task Get_OrganizationNotActive_Returns404OrganizationNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organizationId = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => structure.OrganizationId.ToString(),
        };

        if (how == "deactivated")
        {
            await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Deactivated);
        }

        var answer = await GetAsync(organizationId, structure.ProjectId.ToString(), structure.Platform.Team.Id.ToString());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22,
    // ADR-0026 rule 5): malformed, unknown, of another organization, or deactivated; the project level answers before the team
    // is looked at, so a team that is wrong too changes nothing.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    public async Task Get_ProjectNotReachable_Returns404ProjectNotFound_EvenWithAWrongTeam(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.ProjectId.ToString(),
            _ => structure.ProjectId.ToString(),
        };

        if (how == "deactivated")
        {
            await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);
        }

        var teamId = how == "deactivated" ? structure.Platform.Team.Id.ToString() : "not-a-team";

        var answer = await GetAsync(structure.OrganizationId.ToString(), projectId, teamId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
    }

    // US-4 · "A team identifier that names no team of the project" (BR-M1-20, BR-M1-22) and US-7 · "A deleted team answers not
    // found": malformed, unknown, the team of another project or another organization, and a deleted one.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Get_TeamNotAnActiveTeamOfTheProject_Returns404TeamNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Team.Id);
        var teamId = how switch
        {
            "malformed" => structure.Platform.Team.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => secondProject.FirstTeam.Id.ToString(),
            "of another organization" => other.Platform.Team.Id.ToString(),
            _ => deleted.Team.Id.ToString(),
        };

        var answer = await GetAsync(structure.OrganizationId.ToString(), structure.ProjectId.ToString(), teamId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.team.not_found", answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
    }

    // US-7 · "A deleted team answers not found" does not touch the others: after «Ядро» is deleted, «Платформа» and the first
    // team are still read.
    [Fact]
    public async Task Get_AfterASiblingTeamIsDeleted_OtherTeamsAreStillReturned()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Team.Id);

        var platform = await GetAsync(structure, structure.Platform.Team.Id);
        var first = await GetAsync(structure, structure.FirstTeamId);

        Assert.Equal(HttpStatusCode.OK, platform.Status);
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(structure.RootId, first.Body.GetProperty("defaultArea").GetProperty("id").GetGuid());
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
