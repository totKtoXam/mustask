using System.Net;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/member-candidates?text=</c> (feat-028 api-contract § 2.3a; design § 5.1; the human's
/// gate-3 choice «Серверный поиск сейчас»): the search of the «Участники» field of «Новая команда». It answers what a team's own search
/// answers (mod-016 § 1.3) — the organization's active members found by name or e-mail, at most 20 with <c>moreMatched</c> — for a team
/// that does not exist yet, declares <c>organizations.team.create</c> and asks <c>organizations.team.write</c> as the second code (D-7).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SearchNewTeamMemberCandidatesEndpointTests(MustaskApiFactory factory)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static string Path(Guid organizationId, string? text) =>
        $"{OrganizationsApi.OrgTeams(organizationId)}/member-candidates"
        + (text is null ? string.Empty : $"?text={Uri.EscapeDataString(text)}");

    private Task<Answer> SearchAsync(Guid organizationId, string? text, string? token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(organizationId, text), token: token);

    private static List<(string? Id, string? Name)> Users(Answer answer) =>
        answer.Body.GetProperty("users").EnumerateArray()
            .Select(user => (user.GetProperty("id").GetString(), user.GetProperty("displayName").GetString()))
            .ToList();

    private async Task<(OrganizationStructure Structure, string AdminToken)> WorldAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);

        return (structure, admin.Token);
    }

    // US-1 · "Only active organization members are offered": an active member is, with the e-mail; a member whose account is disabled, a
    // projected user who is a member of nothing, a member REMOVED from the organization and an active member of ANOTHER organization
    // are not. The body has the shape of the team's own search: users (id, displayName, email) and moreMatched.
    [Fact]
    public async Task Search_OffersOnlyActiveMembersOfThisOrganization()
    {
        var tag = Tag();
        var (structure, token) = await WorldAsync();
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var anna = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Участница", "anna@example.org");
        var disabled = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Отключённая", active: false);
        var outsider = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Посторонняя");
        var elsewhere = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Соседняя");
        var removed = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Удалённая");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, anna);
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, disabled);
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, removed);
        await MembershipTestData.AddMemberAsync(factory, other.Id, elsewhere);
        await MembershipTestData.RemoveMemberAsync(factory, structure.OrganizationId, removed);

        var answer = await SearchAsync(structure.OrganizationId, $"Анна{tag}", token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([(anna, $"Анна{tag} Участница")], Users(answer));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
        Assert.Equal(["displayName", "email", "id"], answer.Body.GetProperty("users")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("anna@example.org", answer.Body.GetProperty("users")[0].GetProperty("email").GetString());
        Assert.DoesNotContain(Users(answer), user => user.Id == outsider || user.Id == disabled || user.Id == elsewhere || user.Id == removed);
    }

    // mod-016 § 1.3 · found by name or by e-mail, letter case not deciding, the text trimmed.
    [Fact]
    public async Task Search_ByEmailOrName_IgnoringLetterCase_AndTrimmed()
    {
        var tag = Tag();
        var (structure, token) = await WorldAsync();
        var vera = await WorkItemsTestData.AddUserAsync(factory, "Вера", $"Новикова{tag}", $"Vera.{tag}@Example.org");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, vera);

        var byEmail = await SearchAsync(structure.OrganizationId, $"  vera.{tag.ToUpperInvariant()}@ ", token);
        var byName = await SearchAsync(structure.OrganizationId, $"НОВИКОВА{tag}", token);

        Assert.Equal([vera], Users(byEmail).Select(user => user.Id));
        Assert.Equal([vera], Users(byName).Select(user => user.Id));
    }

    // US-1 · "The search needs two characters": below two (trimmed), an absent text and a text nobody matches all answer 200 with nobody.
    [Theory]
    [InlineData("Никого-Такого-Нет")]
    [InlineData("в")]
    [InlineData("  в  ")]
    [InlineData(null)]
    public async Task Search_NoMatchOrUnderTwoCharacters_OffersNobody(string? text)
    {
        var (structure, token) = await WorldAsync();
        var member = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Короткая");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, member);

        var answer = await SearchAsync(structure.OrganizationId, text, token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Users(answer));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
    }

    // US-1 · "More than twenty matches are cut and said so": 25 active members match, 20 are offered in name order and more matched.
    [Fact]
    public async Task Search_25MembersMatch_Offers20_InOrder_MoreMatched()
    {
        var tag = Tag();
        var (structure, token) = await WorldAsync();
        await UserDirectoryTestData.SeedAsync(
            factory,
            Enumerable.Range(1, 25).Select(index => UserDirectoryTestData.Push($"n{index:D2}-{tag}", $"Кандидат{tag}", $"Номер{index:D2}", null)));
        await WorkItemsTestData.ExecuteAsync(
            factory,
            $"INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) SELECT {structure.OrganizationId}, 'n' || lpad(i::text, 2, '0') || '-' || {tag}, 'member', {OrganizationsTestData.Creator}, now() FROM generate_series(1, 25) i");

        var answer = await SearchAsync(structure.OrganizationId, $"Кандидат{tag}", token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(20, Users(answer).Count);
        Assert.True(answer.Body.GetProperty("moreMatched").GetBoolean());
        Assert.Equal(Enumerable.Range(1, 20).Select(index => $"Кандидат{tag} Номер{index:D2}"), Users(answer).Select(user => user.Name));
    }

    // mod-016 § 0.5 · a text over 100 characters is mod-014's refusal, passed through unchanged.
    [Fact]
    public async Task Search_TextOver100_Returns400_UserDirectoryValidationFailed()
    {
        var (structure, token) = await WorldAsync();

        var answer = await SearchAsync(structure.OrganizationId, new string('а', 101), token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("user_directory.validation_failed", answer.ErrorCode);
    }

    // D-7 · the declared code is the creation's: a member and a viewer, who hold neither `team.create` nor `team.write`, get 403
    // «Недостаточно прав», and an administrator, who holds both, is answered. (The second code, asked by the handler, is proved in
    // SearchNewTeamMemberCandidatesHandlerTests: no bundle holds the one without the other until mod-019.)
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Search_MemberAndViewer_Get403_AdministratorIsAnswered(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);

        var refused = await SearchAsync(structure.OrganizationId, "вера", caller.Token);
        var admitted = await SearchAsync(structure.OrganizationId, "вера", admin.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (refused.Status, refused.ErrorCode));
        Assert.Equal("Недостаточно прав", refused.Detail);
        Assert.Equal(HttpStatusCode.OK, admitted.Status);
    }

    // US-1 · containment: a non-member is told «Организация не найдена» (the answer for an organization that does not exist, byte for
    // byte), an administrator of ANOTHER organization is a non-member of this one, and without a token the answer is 401.
    [Fact]
    public async Task Search_NonMember_Is404_AnAdministratorOfAnotherOrganization_Is404_NoToken_Is401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var elsewhere = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var neighbour = await MembershipTestData.NewMemberAsync(factory, elsewhere.Id, OrganizationRole.Admin);

        var nonMember = await SearchAsync(structure.OrganizationId, "вера", MembershipTestData.Outsider());
        var ofAnother = await SearchAsync(structure.OrganizationId, "вера", neighbour.Token);
        var neverExisted = await SearchAsync(Guid.CreateVersion7(), "вера", neighbour.Token);
        var anonymous = await SearchAsync(structure.OrganizationId, "вера", token: null);

        foreach (var answer in new[] { nonMember, ofAnother, neverExisted })
        {
            Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (answer.Status, answer.ErrorCode));
            Assert.Equal("Организация не найдена.", answer.Detail);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
    }

    // US-1 · containment of the people: one administrator of two organizations asks each of them for a name that both hold under one tag —
    // each address offers its own members and not the other's.
    [Fact]
    public async Task Search_AnAdministratorOfTwoOrganizations_IsOfferedTheMembersOfTheAddressedOne()
    {
        var tag = Tag();
        var a = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var b = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var user = MembershipTestData.NewUserId();
        await MembershipTestData.AddMemberAsync(factory, a.Id, user, OrganizationRole.Admin);
        await MembershipTestData.AddMemberAsync(factory, b.Id, user, OrganizationRole.Admin);
        var token = OrganizationsTokens.SignedIn(user);
        var inA = await WorkItemsTestData.AddUserAsync(factory, $"Общий{tag}", "Из А");
        var inB = await WorkItemsTestData.AddUserAsync(factory, $"Общий{tag}", "Из Б");
        await MembershipTestData.AddMemberAsync(factory, a.Id, inA);
        await MembershipTestData.AddMemberAsync(factory, b.Id, inB);

        var asked = await SearchAsync(a.Id, $"Общий{tag}", token);
        var askedOther = await SearchAsync(b.Id, $"Общий{tag}", token);

        Assert.Equal([inA], Users(asked).Select(candidate => candidate.Id));
        Assert.Equal([inB], Users(askedOther).Select(candidate => candidate.Id));
    }

    // api-contract § 2.3a · the route is the organization's, beside `…/teams/{teamId}`: a literal segment outranks the parameter, so
    // `member-candidates` is never read as a team's id — and the parameter route still answers a real team (any letter case of its id),
    // the sub-routes of a team still read `member-candidates` as an id that names nothing, and the verbs the search does not have fall
    // to the team route: a delete of "member-candidates" is `team.not_found`, not 405.
    [Fact]
    public async Task Route_MemberCandidates_NeverBindsAsATeamId_AndTheTeamRoutesStillAnswer()
    {
        var (structure, token) = await WorldAsync();
        var teams = OrganizationsApi.OrgTeams(structure.OrganizationId);

        var search = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teams}/member-candidates?text=ab", token: token);
        var upperCase = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teams}/Member-Candidates?text=ab", token: token);
        var team = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teams}/{structure.Platform.Id}", token: token);
        var teamUpper = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teams}/{structure.Platform.Id.ToString().ToUpperInvariant()}", token: token);
        var members = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teams}/member-candidates/members", token: token);
        var candidatesOfIt = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teams}/member-candidates/member-candidates?text=ab", token: token);
        var delete = await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, $"{teams}/member-candidates", token: token);

        Assert.Equal(HttpStatusCode.OK, search.Status);
        Assert.True(search.Body.TryGetProperty("users", out _), "The search answers the candidates, not a team.");
        Assert.Equal(HttpStatusCode.OK, upperCase.Status);
        Assert.True(upperCase.Body.TryGetProperty("users", out _));
        Assert.Equal(HttpStatusCode.OK, team.Status);
        Assert.Equal(structure.Platform.Id.ToString(), team.Body.GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.OK, teamUpper.Status);
        Assert.Equal(structure.Platform.Id.ToString(), teamUpper.Body.GetProperty("id").GetString());

        foreach (var answer in new[] { members, candidatesOfIt, delete })
        {
            Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        }
    }
}
