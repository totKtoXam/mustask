using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects</c> (api-contract § 2.1)
/// through the real host and a real PostgreSQL, where the list is ordered by
/// ICU's Russian collation. Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied, so a test asserts on its own organization's list and nothing wider.
/// The organization level of the not-found theory (malformed, unknown,
/// deactivated, a non-member) is the scoped check's, once for every route
/// (<c>ScopedAccessTests</c>, mod-016 ADR-0027); here one test only pins that a
/// deactivated organization does not answer an empty list, and that a restore
/// brings the list back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetProjectsEndpointTests(MustaskApiFactory factory)
{
    private static string ProjectsOf(Guid organizationId) => $"{OrganizationsApi.Organization(organizationId)}/projects";

    private Task<Answer> GetAsync(Guid organizationId, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, ProjectsOf(organizationId), token: token ?? OrganizationsTokens.SignedIn());

    // US-1 · "An organization is created with its process": it has no project yet, and the list is empty, not a refusal.
    [Fact]
    public async Task Get_NewOrganization_ReturnsEmpty()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await GetAsync(organization.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        Assert.Equal(0, answer.Body.GetArrayLength());
    }

    // US-8 · "The switcher lists the active organizations, projects and teams": «Аналитика» and «Пример проекта» in
    // alphabetical order and not the deactivated project; US-3 · each item carries its id, name and key. The order is the
    // database's Russian collation: letters before letter case, «Ё» with «Е» («Ёлка» before «Ель»).
    [Fact]
    public async Task Get_ActiveOnly_InDisplayOrder()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var beta = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Бета", "BET");
        var elk = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Ель", "EL");
        var analytics = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Аналитика", "ANL");
        var alpha = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "альфа", "ALF");
        var fir = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Ёлка", "YOL");
        await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Архив", "ARC", ProjectStatus.Deactivated);

        var answer = await GetAsync(organization.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var items = answer.Body.EnumerateArray().ToList();
        Assert.Equal(
            ["альфа", "Аналитика", "Бета", "Ёлка", "Ель"],
            items.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal(
            [alpha.Project.Id, analytics.Project.Id, beta.Project.Id, fir.Project.Id, elk.Project.Id],
            items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal(["ALF", "ANL", "BET", "YOL", "EL"], items.Select(item => item.GetProperty("key").GetString()));
        Assert.All(items, item => Assert.Equal(["id", "key", "name"], PropertyNames(item)));
    }

    // US-3 · "A project is deactivated": it leaves the active list, nothing else in the list changes; when it is restored
    // it is listed again (BR-M1-24).
    [Fact]
    public async Task Get_ProjectDeactivatedThenRestored_LeavesAndRejoinsTheList()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var example = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Пример проекта", "PRM");
        await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Аналитика", "ANL");

        await OrganizationsTestData.SetProjectStatusAsync(factory, organization.Id, example.Project.Id, ProjectStatus.Deactivated);
        var whileDeactivated = await GetAsync(organization.Id);
        await OrganizationsTestData.SetProjectStatusAsync(factory, organization.Id, example.Project.Id, ProjectStatus.Active);
        var afterRestore = await GetAsync(organization.Id);

        Assert.Equal(["Аналитика"], Names(whileDeactivated));
        Assert.Equal(["Аналитика", "Пример проекта"], Names(afterRestore));
    }

    // US-3 · "A project identifier that names no project of the organization": the projects of another organization are
    // not in the list, and an organization's list does not change with another's (BR-M1-22). The key «PRM» is free in
    // each organization.
    [Fact]
    public async Task Get_ListsOnlyTheProjectsOfTheAddressedOrganization()
    {
        var first = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateOrganizationAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, second.Id, "Отчётность", "RPT");

        var firstList = await GetAsync(first.OrganizationId);
        var secondList = await GetAsync(second.Id);

        Assert.Equal(["Пример проекта"], Names(firstList));
        Assert.Equal(["Отчётность"], Names(secondList));
    }

    // US-1 · "Every signed-in user sees every organization" (BR-M1-19) — since mod-016 every MEMBER of it does, in any role: a
    // viewer with no right at all, and a member who holds the create right, get the same list.
    [Fact]
    public async Task Get_AnyMember_SeesTheSameList()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Viewer);
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);

        var withoutRight = await GetAsync(structure.OrganizationId, viewer.Token);
        var withRight = await GetAsync(structure.OrganizationId, OrganizationsTokens.WithCreateRight(member.Id));

        Assert.Equal(HttpStatusCode.OK, withoutRight.Status);
        Assert.Equal(["Пример проекта"], Names(withoutRight));
        Assert.Equal(HttpStatusCode.OK, withRight.Status);
        Assert.Equal(["Пример проекта"], Names(withRight));
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge comes before the not-found of any level.
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, ProjectsOf(structure.OrganizationId));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    // US-2 · "A deactivated organization takes no change": its projects are not served while it is deactivated, and are
    // served again, as they were, once it is restored (BR-M1-20). The filter answers the 404; the list is not an empty 200.
    [Fact]
    public async Task Get_UnderDeactivatedOrganization_Returns404_UntilRestored()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Архив", "ARC", ProjectStatus.Deactivated);

        await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Deactivated);
        var whileDeactivated = await GetAsync(structure.OrganizationId);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Active);
        var afterRestore = await GetAsync(structure.OrganizationId);

        Assert.Equal(HttpStatusCode.NotFound, whileDeactivated.Status);
        Assert.Equal("organizations.organization.not_found", whileDeactivated.ErrorCode);
        Assert.Equal("Организация не найдена.", whileDeactivated.Detail);
        Assert.Equal(HttpStatusCode.OK, afterRestore.Status);
        Assert.Equal(["Пример проекта"], Names(afterRestore));
    }

    private static IEnumerable<string?> Names(Answer answer) =>
        answer.Body.EnumerateArray().Select(item => item.GetProperty("name").GetString());

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
