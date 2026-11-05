using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}</c> (feat-028 api-contract § 2.1; the route moved out of the project,
/// mod-017 § 3.2 before) through the real host and a real PostgreSQL. Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetTeamEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> GetAsync(string organizationId, string teamId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"/api/organizations/{Uri.EscapeDataString(organizationId)}/teams/{Uri.EscapeDataString(teamId)}",
            token: token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> GetAsync(OrganizationStructure structure, Guid teamId) =>
        GetAsync(structure.OrganizationId.ToString(), teamId.ToString());

    // feat-028 US-1 · "A team is read": the shape is the `Team` of api-contract § 1.1 — id, name, description. A project's view of the
    // team (a mark, a member flag) is the project's list's, not the team's own. Replaces Get_TheProjectsOwnTeam_Returns200_TheTeamShape
    // (the mark and the flag left the shape, D-16).
    [Fact]
    public async Task Get_Returns200_TheTeamShape_NoProjectViewOfIt()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await GetAsync(structure, structure.ProjectTeamId);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["description", "id", "name"], PropertyNames(answer.Body));
        Assert.Equal(structure.ProjectTeamId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal("Пример проекта", answer.Body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("description").ValueKind);
    }

    // feat-028 US-1 · the description is read as typed.
    [Fact]
    public async Task Get_CarriesTheDescriptionAsTyped()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var described = await CreateTeamAsync(factory, structure.OrganizationId, new { name = "Описанная", description = " Платформа\nи инструменты " });

        var answer = await GetAsync(structure, described.Body.GetProperty("id").GetGuid());

        Assert.Equal(" Платформа\nи инструменты ", answer.Body.GetProperty("description").GetString());
    }

    // feat-028 BR-OL-1 · a team linked to no project is read like any other; so is one linked to a deactivated project.
    [Theory]
    [InlineData("linked to no project")]
    [InlineData("linked to a deactivated project")]
    public async Task Get_TeamOfNoActiveProject_Returns200(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var team = how == "linked to no project"
            ? await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Ничья")
            : structure.Platform;

        if (how != "linked to no project")
        {
            await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);
        }

        var answer = await GetAsync(structure, team.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(team.Name, answer.Body.GetProperty("name").GetString());
    }

    // api-contract § 0.2: the canonical form is accepted in either letter case, in every level of the address.
    [Fact]
    public async Task Get_IdsInUpperCase_Returns200()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await GetAsync(
            structure.OrganizationId.ToString().ToUpperInvariant(),
            structure.Platform.Id.ToString().ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(structure.Platform.Id, answer.Body.GetProperty("id").GetGuid());
    }

    // US-1 · "A visitor who is not signed in sees nothing": 401 before any not-found, even for an address that names nothing.
    [Fact]
    public async Task Get_NoToken_Returns401_EvenForAnAddressThatNamesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var known = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Team(structure.OrganizationId, structure.Platform.Id));
        var unknown = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, "/api/organizations/not-an-id/teams/not-a-team");

        Assert.Equal(HttpStatusCode.Unauthorized, known.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.Status);
    }

    // US-2 · "Everything beneath a deactivated organization answers not found" (BR-M1-20): the organization level answers first,
    // whatever the team is.
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

        var answer = await GetAsync(organizationId, structure.Platform.Id.ToString());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);
    }

    // US-1 · "A team identifier that names no team of the organization" (BR-M1-20, BR-M1-22) and US-5 · "A deleted team answers not
    // found": malformed, unknown, the team of another organization, and a deleted one — identical status and body.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Get_TeamNotAnActiveTeamOfTheOrganization_Returns404TeamNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Id);
        var teamId = how switch
        {
            "malformed" => structure.Platform.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Platform.Id.ToString(),
            _ => deleted.Id.ToString(),
        };

        var answer = await GetAsync(structure.OrganizationId.ToString(), teamId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.team.not_found", answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
    }

    // US-5 · "A deleted team answers not found" does not touch the others: after «Ядро» is deleted, «Платформа» and the project's own
    // team are still read.
    [Fact]
    public async Task Get_AfterASiblingTeamIsDeleted_OtherTeamsAreStillReturned()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Id);

        var platform = await GetAsync(structure, structure.Platform.Id);
        var own = await GetAsync(structure, structure.ProjectTeamId);

        Assert.Equal(HttpStatusCode.OK, platform.Status);
        Assert.Equal(HttpStatusCode.OK, own.Status);
        Assert.Equal(structure.ProjectTeamId, own.Body.GetProperty("id").GetGuid());
    }
}
