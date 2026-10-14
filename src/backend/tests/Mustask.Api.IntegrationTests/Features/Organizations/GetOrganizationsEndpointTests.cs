using System.Net;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations</c> (api-contract § 1.1) through the real host and
/// a real PostgreSQL. The integration database is shared by the whole
/// collection and never emptied, so no test asserts the full list: each makes
/// its organization names unique with a tag of its own and looks only at the
/// rows carrying that tag (plan-backend § 13, "The shared database").
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetOrganizationsEndpointTests(MustaskApiFactory factory)
{
    private const string Route = "/api/organizations";

    private Task<Answer> GetAsync(string? token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Route, token: token);

    /// <summary>A tag no other test uses. It leads every name of the test, so the test's rows compare on what follows it.</summary>
    private static string NewTag() => Guid.CreateVersion7().ToString("N");

    /// <summary>The names of the listed organizations that carry <paramref name="tag"/>, in the order received.</summary>
    private static List<string> NamesWith(Answer answer, string tag) =>
        answer.Body.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .Where(name => name.StartsWith(tag, StringComparison.Ordinal))
            .ToList();

    private static List<Guid> Ids(Answer answer) =>
        answer.Body.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

    // US-8 · "Lists in order, deactivated ... left out": the active organizations by name in Russian collation, letters
    // before letter case, «Ё» beside «Е» (design § 4.6, api-contract § 0.6). Ordinal order would put «Ёлка» first and
    // «альфа» last. «Гамма» would sit between «Бета» and «Дуб», but it is deactivated and not listed.
    [Fact]
    public async Task Get_ActiveOnly_InDisplayOrder_YoBesideYe_LetterCaseIgnored()
    {
        var tag = NewTag();
        foreach (var name in new[] { "Жук", "Ель", "Бета", "Ёлка", "альфа", "Дуб" })
        {
            await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} {name}");
        }

        await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} Гамма", OrganizationStatus.Deactivated);

        var answer = await GetAsync(OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(
            new[] { "альфа", "Бета", "Дуб", "Ёлка", "Ель", "Жук" }.Select(name => $"{tag} {name}"),
            NamesWith(answer, tag));
    }

    // api-contract § 1.1: the list is a plain array of { id, name }, and nothing else.
    [Fact]
    public async Task Get_ReturnsAPlainArray_OfIdAndName()
    {
        var tag = NewTag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} Акме");

        var answer = await GetAsync(OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var item = Assert.Single(
            answer.Body.EnumerateArray(),
            candidate => candidate.GetProperty("id").GetGuid() == organization.Id);
        Assert.Equal(["id", "name"], item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal($"{tag} Акме", item.GetProperty("name").GetString());
    }

    // mod-016 US-2 · "The organization list holds only the user's organizations" (BR-M1-7, api-contract § 4.1 row 1): a user who
    // is a member of A and not of B sees A and not B — whoever created either, and whatever right the token carries.
    [Fact]
    public async Task Get_ListsOnlyTheOrganizationsTheCallerIsAMemberOf()
    {
        var tag = NewTag();
        var a = await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} A", founded: false);
        var b = await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} B", founded: false);
        var user = await MembershipTestData.NewMemberAsync(factory, a.Id, Mustask.Domain.Identity.OrganizationRole.Member);
        await MembershipTestData.NewMemberAsync(factory, b.Id, Mustask.Domain.Identity.OrganizationRole.Admin);

        var answer = await GetAsync(user.Token);
        var withCreateRight = await GetAsync(OrganizationsTokens.WithCreateRight(user.Id));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([$"{tag} A"], NamesWith(answer, tag));
        Assert.Contains(a.Id, Ids(answer));
        Assert.DoesNotContain(b.Id, Ids(answer));
        Assert.Equal(NamesWith(answer, tag), NamesWith(withCreateRight, tag));
    }

    // mod-016 US-2 · "in any role": an admin, a member and a viewer of the same organization all see it.
    [Theory]
    [InlineData(Mustask.Domain.Identity.OrganizationRole.Admin)]
    [InlineData(Mustask.Domain.Identity.OrganizationRole.Member)]
    [InlineData(Mustask.Domain.Identity.OrganizationRole.Viewer)]
    public async Task Get_TheCallersOrganization_IsListedInAnyRole(Mustask.Domain.Identity.OrganizationRole role)
    {
        var tag = NewTag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} Акме", founded: false);
        var user = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);

        var answer = await GetAsync(user.Token);

        Assert.Equal([organization.Id], Ids(answer).Where(id => id == organization.Id));
        Assert.Equal([$"{tag} Акме"], NamesWith(answer, tag));
    }

    // mod-016 US-2 · "A user who belongs to no organization sees an explicit empty state" (backend half): an empty list — 200,
    // not an error and not a 404 — for a user with no membership, with or without the create right, however many organizations
    // others have; and an instance administrator is no exception (api-contract § 0.1).
    [Fact]
    public async Task Get_UserWithNoMembership_GetsAnEmptyList_WithOrWithoutTheCreateRight()
    {
        await OrganizationsTestData.CreateOrganizationAsync(factory, $"{NewTag()} Чужая");

        var plain = await GetAsync(OrganizationsTokens.Stranger());
        var creator = await GetAsync(OrganizationsTokens.WithCreateRight());
        var everything = await GetAsync(KeycloakTestRealm.CreateToken([.. Mustask.Domain.Identity.PermissionCatalog.AllCodes], options => options.Subject = OrganizationsTokens.NewSubject()));

        foreach (var answer in new[] { plain, creator, everything })
        {
            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal(0, answer.Body.GetArrayLength());
        }
    }

    // US-2 · "Deactivated ... listed separately": a deactivated organization leaves this list, and the restored one is back
    // under its own name (ADR-0026 rule 4).
    [Fact]
    public async Task Get_DeactivatedOrganization_LeavesTheList_AndReturnsWhenRestored()
    {
        var tag = NewTag();
        var kept = await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} Остаётся");
        var closed = await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} Закрытая");
        var token = OrganizationsTokens.SignedIn();

        await OrganizationsTestData.SetOrganizationStatusAsync(factory, closed.Id, OrganizationStatus.Deactivated);
        var whileDeactivated = await GetAsync(token);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, closed.Id, OrganizationStatus.Active);
        var afterRestore = await GetAsync(token);

        Assert.Equal([$"{tag} Остаётся"], NamesWith(whileDeactivated, tag));
        Assert.DoesNotContain(closed.Id, Ids(whileDeactivated));
        Assert.Contains(kept.Id, Ids(whileDeactivated));
        Assert.Equal([$"{tag} Закрытая", $"{tag} Остаётся"], NamesWith(afterRestore, tag));
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge, before anything else.
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var answer = await GetAsync(token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }
}
