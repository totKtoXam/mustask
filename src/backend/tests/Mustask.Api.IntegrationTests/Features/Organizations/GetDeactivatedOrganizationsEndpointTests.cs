using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/deactivated</c> (api-contract § 1.2) through the
/// real host and a real PostgreSQL (plan-backend § 14.3, lane L-2). The
/// database is shared by the whole collection and never emptied, so no test
/// asserts the full content of the list: it asserts that its own organizations
/// are present, absent or in order, each under a name of its own.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetDeactivatedOrganizationsEndpointTests(MustaskApiFactory factory)
{
    private const string Path = "/api/organizations/deactivated";

    private Task<Answer> GetAsync(string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path, token: token ?? OrganizationsTokens.SignedIn());

    // US-2 · "A deactivated organization is listed separately, with no purge" (BR-M1-1, BR-M1-20): the deactivated ones are
    // listed, the active ones are not, and the display order is by name in Russian collation, ignoring letter case, with
    // «Ё» beside «Е» (design § 4.6). The active organization has a project deactivated on its own: the list goes by the
    // organization's own status alone.
    [Fact]
    public async Task Get_ListsDeactivatedOnly_InDisplayOrder()
    {
        var deactivated = new List<(Organization Organization, string Name)>();

        // Created in an order that is not the display order, and mixing the cases.
        foreach (var prefix in new[] { "Яблоко", "бета", "Ель", "Альфа", "Ёлка" })
        {
            var name = OrganizationsTestData.UniqueName(prefix);
            var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, name, OrganizationStatus.Deactivated);
            deactivated.Add((organization, name));
        }

        var active = await OrganizationsTestData.CreateStructureAsync(factory, OrganizationsTestData.UniqueName("Активная"));
        await OrganizationsTestData.SetProjectStatusAsync(factory, active.OrganizationId, active.ProjectId, ProjectStatus.Deactivated);

        var answer = await GetAsync();

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);

        var items = answer.Body.EnumerateArray().ToList();
        Assert.All(items, item => Assert.Equal(["id", "name"], PropertyNames(item)));
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetGuid() == active.OrganizationId);

        var mine = deactivated.Select(entry => entry.Organization.Id).ToHashSet();
        var listed = items
            .Where(item => mine.Contains(item.GetProperty("id").GetGuid()))
            .Select(item => (Id: item.GetProperty("id").GetGuid(), Name: item.GetProperty("name").GetString()))
            .ToList();

        // ICU Russian: «Альфа» < «бета» (letters before case) < «Ёлка» < «Ель» (the same «Е» up to «Ё», then «к» < «ь») < «Яблоко».
        var expected = new[] { "Альфа", "бета", "Ёлка", "Ель", "Яблоко" }
            .Select(prefix => deactivated.Single(entry => entry.Name.StartsWith(prefix + " ", StringComparison.Ordinal)))
            .Select(entry => (Id: entry.Organization.Id, Name: (string?)entry.Name))
            .ToList();
        Assert.Equal(expected, listed);
    }

    // US-2 · "The organization is deactivated" and "restored": the list follows the organization's status. Deactivating
    // puts it here, restoring takes it out again, and it never has to be created again.
    [Fact]
    public async Task Get_FollowsTheStatus_DeactivatedJoinsAndRestoredLeaves()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var beforeDeactivation = await GetAsync();
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, organization.Id, OrganizationStatus.Deactivated);
        var whileDeactivated = await GetAsync();
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, organization.Id, OrganizationStatus.Active);
        var afterRestore = await GetAsync();

        Assert.False(Contains(beforeDeactivation, organization.Id));
        Assert.True(Contains(whileDeactivated, organization.Id));
        Assert.Equal(organization.Name, NameOf(whileDeactivated, organization.Id));
        Assert.False(Contains(afterRestore, organization.Id));
    }

    // US-2 · "listed separately": the list holds the organization however much sits beneath it, and nothing beneath it is
    // read or changed by the list (ADR-0026 rule 2).
    [Fact]
    public async Task Get_DeactivatedWithStructureBeneath_IsListed_LeavesTheStructureAsItWas()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Deactivated);
        var treeBefore = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        var answer = await GetAsync();

        Assert.True(Contains(answer, structure.OrganizationId));
        Assert.Equal(treeBefore.Count, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-1 · "Every signed-in user sees every organization" (BR-M1-19, C-2): the list is open to any signed-in user, with or
    // without the create right, whoever created the organization.
    [Theory]
    [InlineData("without the create right")]
    [InlineData("with the create right")]
    public async Task Get_AnySignedInUser_SeesTheDeactivatedOnesOfOtherCreators(string how)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, status: OrganizationStatus.Deactivated);
        var token = how == "with the create right" ? OrganizationsTokens.WithCreateRight() : OrganizationsTokens.SignedIn();

        var answer = await GetAsync(token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(Contains(answer, organization.Id));
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge, before anything is read.
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path);

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
    }

    private static bool Contains(Answer answer, Guid organizationId) =>
        answer.Body.EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == organizationId);

    private static string? NameOf(Answer answer, Guid organizationId) =>
        answer.Body.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == organizationId).GetProperty("name").GetString();

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
