using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/teams/{teamId}/members</c> (mod-016 api-contract § 2.3; US-3
/// "An administrator adds an organization member to a team", "Only active members of the organization are offered and accepted",
/// "A user is in a team at most once", doc-006 US-5): a team member is an active member of the organization (BR-M1-10) and the
/// role is unchanged; the add takes the team <c>FOR SHARE</c> and the primary key and the composite key close the races.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AddTeamMemberEndpointTests(MustaskApiFactory factory)
{
    private const string NotInOrganization = "organizations.team.member_not_in_organization";

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static string TeamPath(OrganizationStructure structure, Guid teamId) =>
        $"{OrganizationsApi.OrgTeams(structure.OrganizationId)}/{teamId}/members";

    private Task<Answer> AddAsync(OrganizationStructure structure, Guid teamId, object? body, string? token, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, TeamPath(structure, teamId), body, token, gate);

    private async Task<(OrganizationStructure Structure, string AdminToken, string AdminId)> WorldAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);

        return (structure, admin.Token, admin.Id);
    }

    private async Task<string> MemberUserAsync(OrganizationStructure structure, string firstName, string lastName, string? email = null, bool active = true, OrganizationRole role = OrganizationRole.Member)
    {
        var id = await WorkItemsTestData.AddUserAsync(factory, firstName, lastName, email, active);
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, id, role);

        return id;
    }

    private async Task<OrganizationRole?> RoleOfAsync(Guid organizationId, string userId) =>
        (await MembershipTestData.FindMembershipAsync(factory, organizationId, userId))?.Role;

    // US-3 · "An administrator adds an organization member to a team": 201, the UserRef of the member, the Location of the team's
    // list; the stored row; the member's organization role is unchanged — team membership grants nothing (doc-006 § Roles).
    [Fact]
    public async Task Add_AnActiveMemberOfTheOrganization_Returns201_WithAUserRef_TheRoleUnchanged()
    {
        var tag = Tag();
        var (structure, token, _) = await WorldAsync();
        var vera = await MemberUserAsync(structure, $"Вера{tag}", "Новикова", "vera@example.org", role: OrganizationRole.Viewer);
        var teamId = structure.Platform.Id;

        var answer = await AddAsync(structure, teamId, new { userId = vera }, token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(TeamPath(structure, teamId), answer.Location?.OriginalString);
        Assert.Equal(["displayName", "email", "id", "state"], answer.Body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal((vera, $"Вера{tag} Новикова", "vera@example.org", "active"), (
            answer.Body.GetProperty("id").GetString(),
            answer.Body.GetProperty("displayName").GetString(),
            answer.Body.GetProperty("email").GetString(),
            answer.Body.GetProperty("state").GetString()));
        Assert.Equal([vera], await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
        Assert.Equal(OrganizationRole.Viewer, await RoleOfAsync(structure.OrganizationId, vera));
    }

    // US-3 · the added member is listed at once in the team's members (the list reads the same table), and the add is the only change.
    [Fact]
    public async Task Add_TheMemberIsListedAtOnce_InTheTeamsMembers()
    {
        var (structure, token, _) = await WorldAsync();
        var vera = await MemberUserAsync(structure, "Вера", "Листинг");
        var teamId = structure.Platform.Id;

        await AddAsync(structure, teamId, new { userId = vera }, token);
        var listed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, TeamPath(structure, teamId), token: token);

        Assert.Equal([vera], listed.Body.EnumerateArray().Select(item => item.GetProperty("id").GetString()));
    }

    // US-3 · "Only active members of the organization are offered and accepted" (BR-M1-10, BR-M1-32): a projected user who is not a
    // member, a member of ANOTHER organization, a member whose account is disabled, and an id the projection does not hold are all
    // refused with 409 «Добавить в команду можно только активного участника организации», and nothing is stored.
    [Fact]
    public async Task Add_ANonMemberAnInactiveMemberAndAnUnknownId_Return409MemberNotInOrganization_NothingStored()
    {
        var (structure, token, _) = await WorldAsync();
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var teamId = structure.Platform.Id;
        var projectedOnly = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Посторонняя");
        var elsewhere = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Соседняя");
        await MembershipTestData.AddMemberAsync(factory, other.Id, elsewhere);
        var disabled = await MemberUserAsync(structure, "Олег", "Отключённый", active: false);
        var unknownMember = MembershipTestData.NewUserId();
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, unknownMember);

        foreach (var userId in new[] { projectedOnly, elsewhere, disabled, unknownMember, MembershipTestData.NewUserId() })
        {
            var answer = await AddAsync(structure, teamId, new { userId }, token);

            Assert.Equal((HttpStatusCode.Conflict, NotInOrganization), (answer.Status, answer.ErrorCode));
            Assert.Equal("Добавить в команду можно только активного участника организации", answer.Detail);
        }

        Assert.Empty(await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
    }

    // US-3 · "A user is in a team at most once": 409 «Пользователь уже состоит в команде»; the same user may be in another team.
    [Fact]
    public async Task Add_AUserAlreadyInTheTeam_Returns409MemberExists_AnotherTeamIsFine()
    {
        var (structure, token, _) = await WorldAsync();
        var vera = await MemberUserAsync(structure, "Вера", "Дважды");
        var teamId = structure.Platform.Id;

        var first = await AddAsync(structure, teamId, new { userId = vera }, token);
        var second = await AddAsync(structure, teamId, new { userId = vera }, token);
        var other = await AddAsync(structure, structure.ProjectTeamId, new { userId = vera }, token);

        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal((HttpStatusCode.Conflict, "organizations.team.member_exists"), (second.Status, second.ErrorCode));
        Assert.Equal("Пользователь уже состоит в команде", second.Detail);
        Assert.Equal(HttpStatusCode.Created, other.Status);
        Assert.Equal([vera], await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
    }

    // api-contract § 2.3 · a missing, blank or over-long user id: 400 with the field reason `user_invalid`.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_AMissingOrBlankUserId_Returns400UserInvalid(string? userId)
    {
        var (structure, token, _) = await WorldAsync();

        var answer = await AddAsync(structure, structure.Platform.Id, new { userId }, token);
        var empty = await AddAsync(structure, structure.Platform.Id, new { }, token);
        var tooLong = await AddAsync(structure, structure.Platform.Id, new { userId = new string('u', 256) }, token);

        foreach (var refused in new[] { answer, empty, tooLong })
        {
            Assert.Equal((HttpStatusCode.BadRequest, "organizations.validation_failed"), (refused.Status, refused.ErrorCode));
            Assert.Equal("organizations.membership.user_invalid", refused.Body.GetProperty("errors").GetProperty("userId")[0].GetProperty("code").GetString());
        }
    }

    // api-contract § 2.3 · the team must be an active team of THIS organization: deleted, unknown, malformed and another organization's team answer
    // `team.not_found` (feat-028: the project left the address, so there is no project level any more). Nothing is stored.
    [Fact]
    public async Task Add_ToTheWrongContainerOrADeletedTeam_AnswersTheLevelNotFound()
    {
        var (structure, token, _) = await WorldAsync();
        var vera = await MemberUserAsync(structure, "Вера", "Адресная");
        var elsewhere = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Удалённая");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Id);
        var organization = OrganizationsApi.Organization(structure.OrganizationId);
        var body = new { userId = vera };

        var wrongTeams = new[]
        {
            $"{organization}/teams/{deleted.Id}/members",
            $"{organization}/teams/{Guid.CreateVersion7()}/members",
            $"{organization}/teams/abc/members",
            $"{organization}/teams/{elsewhere.Platform.Id}/members",
        };

        foreach (var path in wrongTeams)
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, path, body, token);

            Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        }

        Assert.Empty(await MembershipTestData.TeamMembershipsOfAsync(factory, vera));
    }

    // US-3 · "The project's creator is the first team's first member" is CreateProject's; here, a creator the projection does not
    // know yet (BR-M1-25) cannot be ADDED — the add needs a known active user — but stays a member of the team they were seeded
    // into. (The seed is the founder's, not this endpoint's.)
    [Fact]
    public async Task Add_AMemberTheProjectionDoesNotKnow_IsRefused_NotInOrganization()
    {
        var (structure, token, _) = await WorldAsync();
        var unknown = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);

        var answer = await AddAsync(structure, structure.Platform.Id, new { userId = unknown.Id }, token);

        Assert.Equal((HttpStatusCode.Conflict, NotInOrganization), (answer.Status, answer.ErrorCode));
    }

    // NFR Concurrency / US-3 · "concurrent duplicate": two administrators add the same user to the same team at the same moment —
    // exactly one 201 and one 409 «уже состоит в команде»; the user is listed once.
    [Fact]
    public async Task Add_TheSameUserTwiceAtTheSameMoment_OneCreated_OneMemberExists_ListedOnce()
    {
        var (structure, token, _) = await WorldAsync();
        var second = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var vera = await MemberUserAsync(structure, "Вера", "Гонка");
        var teamId = structure.Platform.Id;

        var answers = await Task.WhenAll(AddAsync(structure, teamId, new { userId = vera }, token), AddAsync(structure, teamId, new { userId = vera }, second.Token));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], answers.Select(answer => answer.Status).Order());
        Assert.Equal("organizations.team.member_exists", answers.Single(answer => answer.Status == HttpStatusCode.Conflict).ErrorCode);
        Assert.Equal([vera], await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
    }

    // NFR Concurrency / US-3 · "add while the team is deleted", the add first (BR-M1-36): the add is held at its commit with the
    // team locked FOR SHARE; the deletion waits for that lock, and once the add commits it ends the memberships it finds —
    // the new one included. No membership of a deleted team remains.
    [Fact]
    public async Task Add_WhileTheTeamIsDeleted_AddFirst_BothSucceed_NoMembershipOfTheDeletedTeamRemains()
    {
        var (structure, token, _) = await WorldAsync();
        var vera = await MemberUserAsync(structure, "Вера", "Команда");
        var teamId = structure.Platform.Id;
        using var gate = factory.CommitGates.Open();

        var add = AddAsync(structure, teamId, new { userId = vera }, token, gate);
        await Task.WhenAny(gate.Reached, add);
        Assert.False(add.IsCompleted, "The add finished before reaching its commit gate.");

        var deletion = OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.OrgTeams(structure.OrganizationId)}/{teamId}", token: token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, deletion);
        Assert.False(deletion.IsCompleted, "The deletion did not wait for the add's lock on the team.");

        gate.Release();
        var (added, deleted) = (await add, await deletion);

        Assert.Equal(HttpStatusCode.Created, added.Status);
        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Empty(await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
        Assert.Equal(OrganizationRole.Member, await RoleOfAsync(structure.OrganizationId, vera));
    }

    // NFR Concurrency / US-3 · "add while the team is deleted", the deletion first: the deletion is held at its commit with the
    // team locked FOR UPDATE and its memberships ended; the add waits for the lock, re-checks the team after it and answers
    // `team.not_found`. Nothing is stored.
    [Fact]
    public async Task Add_WhileTheTeamIsDeleted_DeletionFirst_TheAddAnswersTeamNotFound_NothingStored()
    {
        var (structure, token, _) = await WorldAsync();
        var vera = await MemberUserAsync(structure, "Вера", "Удаление");
        var teamId = structure.Platform.Id;
        using var gate = factory.CommitGates.Open();

        var deletion = OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.OrgTeams(structure.OrganizationId)}/{teamId}", token: token, gate: gate);
        await Task.WhenAny(gate.Reached, deletion);
        Assert.False(deletion.IsCompleted, "The deletion finished before reaching its commit gate.");

        var add = AddAsync(structure, teamId, new { userId = vera }, token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, add);
        Assert.False(add.IsCompleted, "The add did not wait for the deletion's lock on the team.");

        gate.Release();
        var (deleted, added) = (await deletion, await add);

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (added.Status, added.ErrorCode));
        Assert.Empty(await MembershipTestData.TeamMemberIdsAsync(factory, teamId));
    }

    // US-2 · "A member whose role lacks the code is refused": a member and a viewer get 403; nothing is stored.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Add_MemberAndViewer_Get403_NothingStored(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);
        var vera = await MemberUserAsync(structure, "Вера", "Закрытая");

        var answer = await AddAsync(structure, structure.Platform.Id, new { userId = vera }, caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        Assert.Empty(await MembershipTestData.TeamMemberIdsAsync(factory, structure.Platform.Id));
    }

    // US-2 · "A non-member gets «Организация не найдена»" and 401 without a token.
    [Fact]
    public async Task Add_NonMember_Is404_NoToken_Is401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var vera = await MemberUserAsync(structure, "Вера", "Чужая");
        var body = new { userId = vera };

        var nonMember = await AddAsync(structure, structure.Platform.Id, body, MembershipTestData.Outsider());
        var anonymous = await AddAsync(structure, structure.Platform.Id, body, token: null);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal(JsonValueKind.Object, nonMember.Body.ValueKind);
    }
}
