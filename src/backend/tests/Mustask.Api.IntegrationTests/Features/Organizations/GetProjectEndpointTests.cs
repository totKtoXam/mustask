using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}</c>
/// (api-contract § 2.4) through the real host and a real PostgreSQL. Every test
/// builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetProjectEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> GetAsync(Guid organizationId, string projectId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}",
            token: token ?? OrganizationsTokens.SignedIn());

    // US-3 · "The project settings": the project's name and its key, read-only (BR-M1-23). A signed-in user without any
    // right opens it (BR-M1-19), and the answer is the one project addressed, not a neighbour.
    [Fact]
    public async Task Get_Reachable_ReturnsNameAndKey()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var neighbour = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Платформа данных", "DATA");

        var answer = await GetAsync(structure.OrganizationId, structure.ProjectId.ToString());
        var neighbourAnswer = await GetAsync(structure.OrganizationId, neighbour.Project.Id.ToString());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["id", "key", "name", "organizationId"], PropertyNames(answer.Body));
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.OrganizationId, answer.Body.GetProperty("organizationId").GetGuid());
        Assert.Equal("Пример проекта", answer.Body.GetProperty("name").GetString());
        Assert.Equal("PRM", answer.Body.GetProperty("key").GetString());

        Assert.Equal(HttpStatusCode.OK, neighbourAnswer.Status);
        Assert.Equal(neighbour.Project.Id, neighbourAnswer.Body.GetProperty("id").GetGuid());
        Assert.Equal("Платформа данных", neighbourAnswer.Body.GetProperty("name").GetString());
        Assert.Equal("DATA", neighbourAnswer.Body.GetProperty("key").GetString());
    }

    // api-contract § 0.2: an id in a path is accepted in either letter case, and the response writes it in lowercase.
    [Fact]
    public async Task Get_UpperCaseProjectId_Opens_AnswersLowerCaseId()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await GetAsync(structure.OrganizationId, structure.ProjectId.ToString().ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(structure.ProjectId.ToString(), answer.Body.GetProperty("id").GetString());
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
    }

    // US-3 · "A project identifier that names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown,
    // or the project of another organization all answer project.not_found.
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

    // US-3 · "A project is deactivated": opening its address answers «Проект не найден» and never says "deactivated"
    // (ADR-0026 rule 5); once restored, the same address answers again with the same name and key.
    [Fact]
    public async Task Get_DeactivatedProject_Returns404ProjectNotFound_UntilRestored()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var hidden = await GetAsync(structure.OrganizationId, structure.ProjectId.ToString());

        Assert.Equal(HttpStatusCode.NotFound, hidden.Status);
        Assert.Equal("organizations.project.not_found", hidden.ErrorCode);
        Assert.Equal("Проект не найден.", hidden.Detail);
        Assert.False(hidden.Body.TryGetProperty("params", out _));

        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Active);
        var restored = await GetAsync(structure.OrganizationId, structure.ProjectId.ToString());

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal("Пример проекта", restored.Body.GetProperty("name").GetString());
        Assert.Equal("PRM", restored.Body.GetProperty("key").GetString());
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
