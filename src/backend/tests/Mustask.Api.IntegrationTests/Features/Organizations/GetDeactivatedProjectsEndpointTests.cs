using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/deactivated</c>
/// (api-contract § 2.2) through the real host and a real PostgreSQL. Every test
/// builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied, so no test asserts the content of a
/// list it does not own. The route has no level below the organization, whose
/// not-found theory is the scoped check's (<c>ScopedAccessTests</c>, mod-016 ADR-0027).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetDeactivatedProjectsEndpointTests(MustaskApiFactory factory)
{
    private static string DeactivatedProjects(Guid organizationId) => $"{OrganizationsApi.Organization(organizationId)}/projects/deactivated";

    private Task<Answer> GetAsync(Guid organizationId, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, DeactivatedProjects(organizationId), token: token ?? OrganizationsTokens.SignedIn());

    // US-3 · "A deactivated project is restored exactly as it was": the organization's deactivated projects are listed, the
    // active ones are not, and the item is exactly { id, name, key } (api-contract § 2.1, § 2.2). Deactivating the active
    // project moves it into the list too. The token carries no create right: the caller is the creator, an administrator,
    // who holds organizations.project.deactivate, the code this list is read with (mod-016 D-13).
    [Fact]
    public async Task Get_ListsDeactivatedOnly()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var active = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Действующий", "ACT");
        var archived = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Архивный", "ARC", ProjectStatus.Deactivated);

        var answer = await GetAsync(organization.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        var item = Assert.Single(answer.Body.EnumerateArray());
        Assert.Equal(["id", "key", "name"], PropertyNames(item));
        Assert.Equal(archived.Project.Id.ToString(), item.GetProperty("id").GetString());
        Assert.Equal("Архивный", item.GetProperty("name").GetString());
        Assert.Equal("ARC", item.GetProperty("key").GetString());

        await OrganizationsTestData.SetProjectStatusAsync(factory, organization.Id, active.Project.Id, ProjectStatus.Deactivated);

        var both = await GetAsync(organization.Id);

        var ids = Ids(both);
        Assert.Equal(2, ids.Count);
        Assert.Contains(archived.Project.Id.ToString(), ids);
        Assert.Contains(active.Project.Id.ToString(), ids);
    }

    // US-3 · same scenario with nothing deactivated: an empty array, not a refusal.
    [Fact]
    public async Task Get_NoDeactivatedProject_ReturnsEmptyArray()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Действующий", "ACT");

        var answer = await GetAsync(organization.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        Assert.Empty(answer.Body.EnumerateArray());
    }

    // US-3 · "A deactivated project is restored exactly as it was": restored, it leaves the deactivated list (BR-M1-24).
    [Fact]
    public async Task Get_RestoredProject_LeavesTheList()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Архивный", "ARC", ProjectStatus.Deactivated);
        Assert.Equal([project.Project.Id.ToString()], Ids(await GetAsync(organization.Id)));

        await OrganizationsTestData.SetProjectStatusAsync(factory, organization.Id, project.Project.Id, ProjectStatus.Active);

        Assert.Empty(Ids(await GetAsync(organization.Id)));
    }

    // BR-M1-22: a deactivated project of another organization is not in this organization's list, and this one's is not in theirs.
    [Fact]
    public async Task Get_DeactivatedProjectOfAnotherOrganization_IsNotListed()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var mine = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Мой архивный", "MINE", ProjectStatus.Deactivated);
        var theirs = await OrganizationsTestData.CreateProjectAsync(factory, other.Id, "Чужой архивный", "THEIRS", ProjectStatus.Deactivated);

        var answer = await GetAsync(organization.Id);
        var theirAnswer = await GetAsync(other.Id);

        Assert.Equal([mine.Project.Id.ToString()], Ids(answer));
        Assert.Equal([theirs.Project.Id.ToString()], Ids(theirAnswer));
    }

    // api-contract § 0.6: the server orders the list by name in Russian collation, ignoring letter case, «Ё» with «Е»,
    // whatever the order of creation (design § 4.6).
    [Fact]
    public async Task Get_Deactivated_InDisplayOrder()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        foreach (var (name, key) in new[] { ("Ель", "ELK"), ("Бета", "BET"), ("Ёлка", "YOL"), ("альфа", "ALF") })
        {
            await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, name, key, ProjectStatus.Deactivated);
        }

        var answer = await GetAsync(organization.Id);

        Assert.Equal(["альфа", "Бета", "Ёлка", "Ель"], answer.Body.EnumerateArray().Select(item => item.GetProperty("name").GetString()));
    }

    // ADR-0026 rule 5 and US-2 · "A deactivated organization takes no change; an unknown one answers not found": under a
    // deactivated organization its deactivated projects are not listed, the answer is the organization's not-found.
    [Fact]
    public async Task Get_UnderDeactivatedOrganization_Returns404OrganizationNotFound()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Архивный", "ARC", ProjectStatus.Deactivated);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, organization.Id, OrganizationStatus.Deactivated);

        var answer = await GetAsync(organization.Id);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Архивный", "ARC", ProjectStatus.Deactivated);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, DeactivatedProjects(organization.Id));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
    }

    private static IReadOnlyList<string?> Ids(Answer answer) =>
        answer.Body.EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToList();

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
