using System.Net;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/member-candidates?text=</c> (mod-016
/// api-contract § 2.2; US-3 "Only active members of the organization are offered and accepted", US-1 "Past 500 ids … the member
/// searches still answer"): the search of «Добавление в команду» offers active members of this organization only — found by name or
/// e-mail in the user projection, in batches above 500 ids — and is an administrator's control (<c>organizations.team.write</c>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SearchTeamMemberCandidatesEndpointTests(MustaskApiFactory factory)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static string Path(OrganizationStructure structure, string? text, string? teamId = null) =>
        $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{teamId ?? structure.Platform.Team.Id.ToString()}/member-candidates"
        + (text is null ? string.Empty : $"?text={Uri.EscapeDataString(text)}");

    private Task<Answer> SearchAsync(OrganizationStructure structure, string? text, string token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(structure, text), token: token);

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

    // US-3 · "Only active members of the organization are offered": an active member is offered, with the e-mail; a disabled member,
    // a projected user who is a member of nothing, and an active member of ANOTHER organization are not.
    [Fact]
    public async Task Search_OffersOnlyActiveMembersOfThisOrganization()
    {
        var tag = Tag();
        var (structure, token) = await WorldAsync();
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var anna = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Участница", "anna@example.org");
        var oleg = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Отключённая", active: false);
        var outsider = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Посторонняя");
        var elsewhere = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Соседняя");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, anna);
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, oleg);
        await MembershipTestData.AddMemberAsync(factory, other.Id, elsewhere);

        var answer = await SearchAsync(structure, $"Анна{tag}", token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([(anna, $"Анна{tag} Участница")], Users(answer));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
        Assert.Equal(["displayName", "email", "id"], answer.Body.GetProperty("users")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("anna@example.org", answer.Body.GetProperty("users")[0].GetProperty("email").GetString());
        Assert.DoesNotContain(Users(answer), user => user.Id == outsider || user.Id == oleg || user.Id == elsewhere);
    }

    // US-3 · "found by name or e-mail", and "a user already in the team is offered and refused on add" (api-contract § 2.2).
    [Fact]
    public async Task Search_ByEmail_OffersTheMember_AUserAlreadyInTheTeamIncluded()
    {
        var tag = Tag();
        var (structure, token) = await WorldAsync();
        var vera = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Командная", $"vera.{tag}@example.org");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, vera);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.Platform.Team.Id, vera);

        var answer = await SearchAsync(structure, $"vera.{tag}@", token);

        Assert.Equal([vera], Users(answer).Select(user => user.Id));
    }

    // US-3 · "Пользователи не найдены": nobody matches — 200 with an empty list; and below two characters nobody is offered.
    [Theory]
    [InlineData("Никого-Такого-Нет")]
    [InlineData("в")]
    [InlineData(null)]
    public async Task Search_NoMatchOrUnderTwoCharacters_OffersNobody(string? text)
    {
        var (structure, token) = await WorldAsync();
        var member = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Короткая");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, member);

        var answer = await SearchAsync(structure, text, token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Users(answer));
        Assert.False(answer.Body.GetProperty("moreMatched").GetBoolean());
    }

    // US-1 · "More than twenty matches" for members: 25 active members match, 20 are offered in order and more matched.
    [Fact]
    public async Task Search_25MembersMatch_Offers20_InOrder_MoreMatched()
    {
        var tag = Tag();
        var (structure, token) = await WorldAsync();
        await UserDirectoryTestData.SeedAsync(
            factory,
            Enumerable.Range(1, 25).Select(index => UserDirectoryTestData.Push($"k{index:D2}-{tag}", $"Кандидат{tag}", $"Номер{index:D2}", null)));
        await WorkItemsTestData.ExecuteAsync(
            factory,
            $"INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) SELECT {structure.OrganizationId}, 'k' || lpad(i::text, 2, '0') || '-' || {tag}, 'member', {OrganizationsTestData.Creator}, now() FROM generate_series(1, 25) i");

        var answer = await SearchAsync(structure, $"Кандидат{tag}", token);

        Assert.Equal(20, Users(answer).Count);
        Assert.True(answer.Body.GetProperty("moreMatched").GetBoolean());
        Assert.Equal(Enumerable.Range(1, 20).Select(index => $"Кандидат{tag} Номер{index:D2}"), Users(answer).Select(user => user.Name));
    }

    // US-1 · "Past 500 ids … the member searches still answer" (NFR Volumes, mod-014 BR-UP-17): in an organization of 501 members
    // the search restricted to members finds the one it is asked for — in whichever batch of 500 it falls — and a non-member who
    // matches the same text is not offered. The search never refuses for the size of the organization.
    [Fact]
    public async Task Search_AnOrganizationOf501Members_FindsAMemberInAnyBatch_AndNoNonMember()
    {
        var tag = Tag();
        var (structure, token) = await WorldAsync();
        var pushes = Enumerable.Range(0, 501).Select(i => UserDirectoryTestData.Push($"s{i:D3}-{tag}", $"Участник{tag}", $"{i:D3}", null)).ToList();
        pushes.Add(UserDirectoryTestData.Push($"outsider-{tag}", $"Посторонний{tag}", "Никто", null));
        await UserDirectoryTestData.SeedAsync(factory, pushes);
        await WorkItemsTestData.ExecuteAsync(
            factory,
            $"INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) SELECT {structure.OrganizationId}, 's' || lpad(i::text, 3, '0') || '-' || {tag}, 'member', {OrganizationsTestData.Creator}, now() FROM generate_series(0, 500) i");

        var first = await SearchAsync(structure, $"Участник{tag} 000", token);
        var last = await SearchAsync(structure, $"Участник{tag} 500", token);
        var outsider = await SearchAsync(structure, $"Посторонний{tag}", token);

        Assert.Equal(HttpStatusCode.OK, last.Status);
        Assert.Equal([$"s000-{tag}"], Users(first).Select(user => user.Id));
        Assert.Equal([$"s500-{tag}"], Users(last).Select(user => user.Id));
        Assert.Empty(Users(outsider));
    }

    // api-contract § 0.5: a text over 100 characters is mod-014's refusal, passed through unchanged.
    [Fact]
    public async Task Search_TextOver100_Returns400_UserDirectoryValidationFailed()
    {
        var (structure, token) = await WorldAsync();

        var answer = await SearchAsync(structure, new string('а', 101), token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("user_directory.validation_failed", answer.ErrorCode);
    }

    // api-contract § 2 · the team must be reachable: a deleted team, an unknown one and a malformed one answer `team.not_found`; an
    // unknown project `project.not_found`.
    [Fact]
    public async Task Search_ADeletedOrUnknownTeamAndProject_AnswerTheirLevelNotFound()
    {
        var (structure, token) = await WorldAsync();
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Team.Id);

        var deleted = await SearchAsync(structure, "вера", token);
        var unknownTeam = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(structure, "вера", Guid.CreateVersion7().ToString()), token: token);
        var malformed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(structure, "вера", "abc"), token: token);
        var unknownProject = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Organization(structure.OrganizationId)}/projects/{Guid.CreateVersion7()}/teams/{structure.FirstTeamId}/member-candidates?text=вера",
            token: token);

        foreach (var answer in new[] { deleted, unknownTeam, malformed })
        {
            Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        }

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (unknownProject.Status, unknownProject.ErrorCode));
    }

    // US-2 · "A member whose role lacks the code is refused": the search is a write control — a member and a viewer get 403.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Search_MemberAndViewer_Get403(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);

        var answer = await SearchAsync(structure, "вера", caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
    }

    // US-2 · "A non-member gets «Организация не найдена»", and 401 without a token.
    [Fact]
    public async Task Search_NonMember_Is404_NoToken_Is401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var nonMember = await SearchAsync(structure, "вера", MembershipTestData.Outsider());
        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(structure, "вера"));

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
    }
}
