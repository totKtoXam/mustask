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

    // US-1 · "Every signed-in user sees every organization" (BR-M1-19, api-contract § 0.1): a user holding no right at all
    // sees the organizations of other creators, and so does another user with the create right.
    [Fact]
    public async Task Get_UserWithoutRight_SeesOrganizationsOfOtherCreators()
    {
        var tag = NewTag();
        var first = await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} Первая");
        var second = await OrganizationsTestData.CreateOrganizationAsync(factory, $"{tag} Вторая");
        var userWithoutRight = OrganizationsTokens.NewSubject();
        Assert.NotEqual(OrganizationsTestData.Creator, userWithoutRight);

        var plain = await GetAsync(OrganizationsTokens.SignedIn(userWithoutRight));
        var creator = await GetAsync(OrganizationsTokens.WithCreateRight());

        Assert.Equal(HttpStatusCode.OK, plain.Status);
        Assert.Equal([$"{tag} Вторая", $"{tag} Первая"], NamesWith(plain, tag));
        Assert.Contains(first.Id, Ids(plain));
        Assert.Contains(second.Id, Ids(plain));
        Assert.Equal(HttpStatusCode.OK, creator.Status);
        Assert.Equal(NamesWith(plain, tag), NamesWith(creator, tag));
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
