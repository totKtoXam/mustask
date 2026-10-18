using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/members</c> (mod-016 api-contract § 2.1; US-3
/// "A team with no members", "An inactive or unknown team member stays listed"): the team's members as people, in the order of
/// § 0.5, from the user projection and never from Keycloak; a team that is deleted or addressed through another project or
/// organization is a team that is not found.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetTeamMembersEndpointTests(MustaskApiFactory factory)
{
    private const string OrganizationNotFound = "organizations.organization.not_found";
    private const string ProjectNotFound = "organizations.project.not_found";
    private const string TeamNotFound = "organizations.team.not_found";

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static string Path(Guid organizationId, string teamId) =>
        $"{OrganizationsApi.Organization(organizationId)}/teams/{Uri.EscapeDataString(teamId)}/members";

    private Task<Answer> GetAsync(OrganizationStructure structure, Guid teamId, string token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(structure.OrganizationId, teamId.ToString()), token: token);

    private static List<JsonElement> Items(Answer answer) => answer.Body.EnumerateArray().ToList();

    private static string? Name(JsonElement reference) =>
        reference.GetProperty("displayName").ValueKind == JsonValueKind.Null ? null : reference.GetProperty("displayName").GetString();

    private async Task<(OrganizationStructure Structure, MembershipTestData.TestMember Reader)> WorldAsync(OrganizationRole reader = OrganizationRole.Viewer)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        return (structure, await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, reader));
    }

    // api-contract § 2.1: the shape of a UserRef — nothing else about a person is offered.
    [Fact]
    public async Task Get_ATeamMember_HasTheUserRefShape()
    {
        var tag = Tag();
        var (structure, reader) = await WorldAsync();
        var vera = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, vera, $"Вера{tag}", "Новикова", "vera@example.org");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, vera);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.Platform.Id, vera);

        var answer = await GetAsync(structure, structure.Platform.Id, reader.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        var item = Assert.Single(Items(answer));
        Assert.Equal(["displayName", "email", "id", "state"], item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal((vera, $"Вера{tag} Новикова", "vera@example.org", "active"), (
            item.GetProperty("id").GetString(), Name(item), item.GetProperty("email").GetString(), item.GetProperty("state").GetString()));
    }

    // US-3 · "A team with no members": an empty array, 200 — the copy is the panel's.
    [Fact]
    public async Task Get_ATeamWithNoMembers_Returns200WithAnEmptyArray()
    {
        var (structure, reader) = await WorldAsync();

        var answer = await GetAsync(structure, structure.Platform.Id, reader.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        Assert.Empty(Items(answer));
    }

    // US-3 · "An inactive or unknown team member stays listed" (BR-M1-17): a disabled member is shown `inactive`, one the
    // projection does not hold `unknown` with no name or e-mail; unknown users come last, by id. Every role reads.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Get_InactiveAndUnknownMembers_StayListed_InTheOrderOfTheContract(OrganizationRole role)
    {
        var tag = Tag();
        var (structure, reader) = await WorldAsync(role);
        var teamId = structure.Platform.Id;
        var boris = MembershipTestData.NewUserId();
        var anna = MembershipTestData.NewUserId();
        var oleg = MembershipTestData.NewUserId();
        var unknown = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, boris, $"Борис{tag}", "Иванов");
        await MembershipTestData.ProjectUserAsync(factory, anna, $"Анна{tag}", "Смирнова");
        await MembershipTestData.ProjectUserAsync(factory, oleg, $"Олег{tag}", "Отключённый", active: false);

        foreach (var id in new[] { boris, unknown, oleg, anna })
        {
            await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, id);
            await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, id);
        }

        var answer = await GetAsync(structure, teamId, reader.Token);

        var items = Items(answer);
        Assert.Equal([anna, boris, oleg, unknown], items.Select(item => item.GetProperty("id").GetString()));
        Assert.Equal(["active", "active", "inactive", "unknown"], items.Select(item => item.GetProperty("state").GetString()));
        Assert.Equal($"Олег{tag} Отключённый", Name(items[2]));
        Assert.Equal(JsonValueKind.Null, items[3].GetProperty("displayName").ValueKind);
        Assert.Equal(JsonValueKind.Null, items[3].GetProperty("email").ValueKind);
    }

    // US-3 · the list is the team's only: members of another team of the project, and of other organizations' teams, are absent.
    [Fact]
    public async Task Get_ListsOnlyThisTeamsMembers()
    {
        var (structure, reader) = await WorldAsync();
        var here = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var inFirstTeam = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.Platform.Id, here.Id);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.ProjectTeamId, inFirstTeam.Id);

        var answer = await GetAsync(structure, structure.Platform.Id, reader.Token);

        Assert.Equal([here.Id], Items(answer).Select(item => item.GetProperty("id").GetString()));
    }

    // US-3 · "A deleted team's memberships end": the team's member list then answers `team.not_found`.
    [Fact]
    public async Task Get_ADeletedTeam_Returns404TeamNotFound()
    {
        var (structure, reader) = await WorldAsync();
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.Platform.Id, member.Id);
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Id);

        var answer = await GetAsync(structure, structure.Platform.Id, reader.Token);

        Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (answer.Status, answer.ErrorCode));
    }

    // feat-028 api-contract § 2.3 · the team must be an active team of THIS organization: a malformed id, an unknown one and a team of another
    // organization answer `team.not_found`; a team linked to no project of its own organization is read all the same (BR-OL-1).
    [Fact]
    public async Task Get_ATeamOfAnotherOrganizationOrUnknown_AnswersTeamNotFound_AnUnlinkedTeamIsRead()
    {
        var (structure, reader) = await WorldAsync();
        var elsewhere = await OrganizationsTestData.CreateStructureAsync(factory);
        var unlinked = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Без проекта");

        foreach (var team in new[] { "abc", Guid.CreateVersion7().ToString(), Guid.Empty.ToString(), elsewhere.Platform.Id.ToString() })
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(structure.OrganizationId, team), token: reader.Token);

            Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (answer.Status, answer.ErrorCode));
        }

        var read = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Path(structure.OrganizationId, unlinked.Id.ToString()), token: reader.Token);

        Assert.Equal(HttpStatusCode.OK, read.Status);
    }

    // US-1 "Past 500 ids the list … still answers" for the team's list: 501 members, a few of the 501 in the team — all listed,
    // none refused for the size of the organization.
    [Fact]
    public async Task Get_AnOrganizationOf501Members_AllTheTeamsMembersAreListed()
    {
        var tag = Tag();
        var (structure, reader) = await WorldAsync();
        var pushes = Enumerable.Range(0, 501).Select(i => UserDirectoryTestData.Push($"t{i:D3}-{tag}", $"Участник{tag}", $"{i:D3}", null)).ToList();
        await UserDirectoryTestData.SeedAsync(factory, pushes);
        await WorkItems.WorkItemsTestData.ExecuteAsync(
            factory,
            $"INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) SELECT {structure.OrganizationId}, 't' || lpad(i::text, 3, '0') || '-' || {tag}, 'member', {OrganizationsTestData.Creator}, now() FROM generate_series(0, 500) i");
        await WorkItems.WorkItemsTestData.ExecuteAsync(
            factory,
            $"INSERT INTO team_memberships (team_id, organization_id, user_id) SELECT {structure.Platform.Id}, {structure.OrganizationId}, user_id FROM organization_memberships WHERE organization_id = {structure.OrganizationId} AND user_id LIKE 't%-' || {tag}");

        var answer = await GetAsync(structure, structure.Platform.Id, reader.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(501, Items(answer).Count);
        Assert.All(Items(answer), item => Assert.NotNull(Name(item)));
    }

    // US-2 · "A non-member gets «Организация не найдена»", and 401 without a token.
    [Fact]
    public async Task Get_NonMember_Is404_NoToken_Is401()
    {
        var (structure, _) = await WorldAsync();
        var path = Path(structure.OrganizationId, structure.Platform.Id.ToString());

        var nonMember = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: MembershipTestData.Outsider());
        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path);

        Assert.Equal((HttpStatusCode.NotFound, OrganizationNotFound), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
    }
}
