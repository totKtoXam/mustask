using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/member-candidates?text=</c> (mod-016 api-contract § 1.3; US-1 "The search needs two
/// characters", "More than twenty matches", "An inactive or unknown user is neither offered nor added"): the «Добавление
/// участника» search reads the user projection only — works with Keycloak stopped, which these tests do not even have — offers
/// active users whoever they already are, and is an administrator's control (<c>organizations.membership.write</c>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SearchMemberCandidatesEndpointTests(MustaskApiFactory factory)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static string Path(Guid organizationId, string? text) =>
        $"{OrganizationsApi.Organization(organizationId)}/member-candidates" + (text is null ? string.Empty : $"?text={Uri.EscapeDataString(text)}");

    private Task<Answer> SearchAsync(Guid organizationId, string? text, string token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organizationId, text), token: token);

    private static List<(string? Id, string? Name, string? Email)> Users(Answer answer) =>
        answer.Body.GetProperty("users").EnumerateArray()
            .Select(user => (user.GetProperty("id").GetString(), user.GetProperty("displayName").GetString(), user.GetProperty("email").GetString()))
            .ToList();

    private async Task<(Guid OrganizationId, string AdminToken)> OrganizationWithAdminAsync()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);

        return (organization.Id, admin.Token);
    }

    // US-1 · "An administrator adds a member from «Участники»": the search finds an active user by name, with the e-mail, in the
    // shape of the assignee picker (api-contract § 1.3) — and nothing more about the person.
    [Fact]
    public async Task Search_ByName_OffersTheActiveUser_InThePickersShape()
    {
        var tag = Tag();
        var (organizationId, token) = await OrganizationWithAdminAsync();
        var vera = await WorkItemsTestData.AddUserAsync(factory, $"Вера{tag}", "Новикова", "vera.novikova@example.org");

        var answer = await SearchAsync(organizationId, $"Вера{tag}", token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([(vera, $"Вера{tag} Новикова", "vera.novikova@example.org")], Users(answer));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
        Assert.Equal(["moreMatched", "users"], answer.Body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["displayName", "email", "id"], answer.Body.GetProperty("users")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    // US-1 · "The search is also by e-mail".
    [Fact]
    public async Task Search_ByEmail_OffersTheUser()
    {
        var tag = Tag();
        var (organizationId, token) = await OrganizationWithAdminAsync();
        var vera = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Почтова", $"vera.{tag}@example.org");

        var answer = await SearchAsync(organizationId, $"vera.{tag}@", token);

        Assert.Equal([vera], Users(answer).Select(user => user.Id));
    }

    // US-1 · "The search needs two characters": one character, no text, and a text that normalizes to nothing answer nobody —
    // 200 with an empty list, not an error ("no request is sent" below two characters is the panel's).
    [Theory]
    [InlineData("в")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Search_UnderTwoCharacters_OffersNobody(string? text)
    {
        var (organizationId, token) = await OrganizationWithAdminAsync();
        await WorkItemsTestData.AddUserAsync(factory, "Вера", "Короткова");

        var answer = await SearchAsync(organizationId, text, token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Users(answer));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
    }

    // US-1 · "More than twenty matches": 25 active users match, exactly 20 are offered and the answer says more matched.
    [Fact]
    public async Task Search_25Match_Offers20_InOrder_MoreMatched()
    {
        var tag = Tag();
        var (organizationId, token) = await OrganizationWithAdminAsync();
        await UserDirectoryTestData.SeedAsync(
            factory,
            Enumerable.Range(1, 25).Select(index => UserDirectoryTestData.Push($"c{index:D2}-{tag}", $"Кандидат{tag}", $"Номер{index:D2}", null)));

        var answer = await SearchAsync(organizationId, $"Кандидат{tag}", token);

        var names = Users(answer).Select(user => user.Name).ToList();
        Assert.Equal(20, names.Count);
        Assert.True(answer.Body.GetProperty("moreMatched").GetBoolean());
        Assert.Equal(Enumerable.Range(1, 20).Select(index => $"Кандидат{tag} Номер{index:D2}"), names);
    }

    // US-1 · "An inactive or unknown user is neither offered nor added" (BR-M1-32, mod-014 BR-UP-17): a disabled account is never
    // offered, and a user the projection does not hold cannot be found at all.
    [Fact]
    public async Task Search_InactiveUser_IsNotOffered_AnUnknownOneCannotBeFound()
    {
        var tag = Tag();
        var (organizationId, token) = await OrganizationWithAdminAsync();
        await WorkItemsTestData.AddUserAsync(factory, $"Пётр{tag}", "Отключённый", $"petr.{tag}@example.org", active: false);

        Assert.Empty(Users(await SearchAsync(organizationId, $"Пётр{tag}", token)));
        Assert.Empty(Users(await SearchAsync(organizationId, $"petr.{tag}", token)));
        Assert.Empty(Users(await SearchAsync(organizationId, $"Неизвестный{tag}", token)));
    }

    // api-contract § 1.3 / doc-006 US-3 · "an existing member is offered and refused on add": the search says nothing of who is
    // already a member, of this organization or of another.
    [Fact]
    public async Task Search_AnExistingMemberAndAMemberOfAnotherOrganization_AreOffered()
    {
        var tag = Tag();
        var (organizationId, token) = await OrganizationWithAdminAsync();
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var here = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Здесь");
        var elsewhere = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Там");
        await MembershipTestData.AddMemberAsync(factory, organizationId, here);
        await MembershipTestData.AddMemberAsync(factory, other.Id, elsewhere);

        var answer = await SearchAsync(organizationId, $"Анна{tag}", token);

        Assert.Equal(new[] { here, elsewhere }.Order(StringComparer.Ordinal), Users(answer).Select(user => user.Id).Order(StringComparer.Ordinal));
    }

    // api-contract § 0.5: a text over 100 characters is mod-014's refusal, passed through unchanged.
    [Fact]
    public async Task Search_TextOver100_Returns400_UserDirectoryValidationFailed()
    {
        var (organizationId, token) = await OrganizationWithAdminAsync();

        var answer = await SearchAsync(organizationId, new string('а', 101), token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("user_directory.validation_failed", answer.ErrorCode);
        Assert.Equal("user_directory.value_too_long", answer.Body.GetProperty("errors").GetProperty("text")[0].GetProperty("code").GetString());
    }

    // US-2 · "A member whose role lacks the code is refused": the search is a write control — a member and a viewer get 403.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Search_MemberAndViewer_Get403(OrganizationRole role)
    {
        var tag = Tag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);
        await WorkItemsTestData.AddUserAsync(factory, $"Вера{tag}", "Закрытая");

        var answer = await SearchAsync(organization.Id, $"Вера{tag}", caller.Token);

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Equal("organizations.access.forbidden", answer.ErrorCode);
    }

    // US-2 · "A non-member gets «Организация не найдена»" and 401 without a token.
    [Fact]
    public async Task Search_NonMember_Is404_NoToken_Is401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var nonMember = await SearchAsync(organization.Id, "вера", MembershipTestData.Outsider());
        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organization.Id, "вера"));

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal(JsonValueKind.Object, nonMember.Body.ValueKind);
    }
}
