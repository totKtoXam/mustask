using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/members</c> (mod-016 api-contract § 1.4; US-1 "An administrator adds a member from
/// «Участники»", "A role outside the three is refused", "An inactive or unknown user is neither offered nor added", doc-006 US-3
/// scenarios 1–6): the reference use case of mod-016's slices. The scoped check answers 401, 404 and 403 before the body is read;
/// the handler then answers the values (every field reason at once), the user through the projection (<c>user_unavailable</c>),
/// the duplicate (<c>already_member</c>) and saves — effective on the added user's next request.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AddMemberEndpointTests(MustaskApiFactory factory)
{
    private const string ValidationFailed = "organizations.validation_failed";

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static string MembersPath(Guid organizationId) => $"{OrganizationsApi.Organization(organizationId)}/members";

    private Task<Answer> AddAsync(Guid organizationId, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, MembersPath(organizationId), body, token);

    private async Task<MembershipTestData.TestMember> AdminAsync(Guid organizationId, string? firstName = null, string? lastName = null)
    {
        var admin = await MembershipTestData.NewMemberAsync(factory, organizationId, OrganizationRole.Admin);

        if (firstName is not null)
        {
            await MembershipTestData.ProjectUserAsync(factory, admin.Id, firstName, lastName ?? "Админов");
        }

        return admin;
    }

    private static string? Name(JsonElement reference) =>
        reference.GetProperty("displayName").ValueKind == JsonValueKind.Null ? null : reference.GetProperty("displayName").GetString();

    private static string FieldReason(Answer answer, string field) =>
        answer.Body.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString()!;

    // US-1 · "An administrator adds a member from «Участники»": 201 with the Member shape, the Location of the list, the caller as
    // «Добавлен» with the time, no role change; the stored row says the same.
    [Fact]
    public async Task Add_AKnownActiveUser_Returns201_WithTheMemberShapeTheLocationAndWhoAdded()
    {
        var tag = Tag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id, $"Анна{tag}", "Админова");
        var vera = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, vera, $"Вера{tag}", "Новикова", "vera@example.org");
        var before = DateTimeOffset.UtcNow;

        var answer = await AddAsync(organization.Id, new { userId = vera, role = "member" }, admin.Token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(MembersPath(organization.Id), answer.Location?.OriginalString);
        Assert.Equal(["addedAt", "addedBy", "role", "roleChangedAt", "roleChangedBy", "user"], answer.Body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal((vera, $"Вера{tag} Новикова", "vera@example.org", "active"), (
            answer.Body.GetProperty("user").GetProperty("id").GetString(),
            Name(answer.Body.GetProperty("user")),
            answer.Body.GetProperty("user").GetProperty("email").GetString(),
            answer.Body.GetProperty("user").GetProperty("state").GetString()));
        Assert.Equal("member", answer.Body.GetProperty("role").GetString());
        Assert.Equal((admin.Id, $"Анна{tag} Админова"), (answer.Body.GetProperty("addedBy").GetProperty("id").GetString(), Name(answer.Body.GetProperty("addedBy"))));
        Assert.InRange(answer.Body.GetProperty("addedAt").GetDateTimeOffset(), before.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("roleChangedBy").ValueKind);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("roleChangedAt").ValueKind);

        var stored = await MembershipTestData.FindMembershipAsync(factory, organization.Id, vera);
        Assert.NotNull(stored);
        Assert.Equal((OrganizationRole.Member, admin.Id, (string?)null), (stored.Role, stored.AddedBy, stored.RoleChangedBy));
    }

    // US-1 · "A role is chosen from the three": each of admin, member and viewer is stored as asked.
    [Theory]
    [InlineData("admin", OrganizationRole.Admin)]
    [InlineData("member", OrganizationRole.Member)]
    [InlineData("viewer", OrganizationRole.Viewer)]
    public async Task Add_EachOfTheThreeRoles_IsStoredAsAsked(string role, OrganizationRole expected)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Роль", "Проверяемая");

        var answer = await AddAsync(organization.Id, new { userId = user, role }, admin.Token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(role, answer.Body.GetProperty("role").GetString());
        Assert.Equal(expected, (await MembershipTestData.FindMembershipAsync(factory, organization.Id, user))!.Role);
    }

    // doc-006 US-3 scenario 1 / BR-M1-6 · "effective on the next request": the added user's own token, unchanged, was a stranger's
    // before the add (404) and reads the organization's access after it, with the role's exact bundle.
    [Fact]
    public async Task Add_TakesEffectOnTheAddedUsersNextRequest_WithTheSameToken()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Новая");
        var token = OrganizationsTokens.SignedIn(user);
        var accessPath = $"{OrganizationsApi.Organization(organization.Id)}/access";

        var before = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, accessPath, token: token);
        await AddAsync(organization.Id, new { userId = user, role = "viewer" }, admin.Token);
        var after = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, accessPath, token: token);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (before.Status, before.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, after.Status);
        Assert.Equal("viewer", after.Body.GetProperty("role").GetString());
    }

    // US-1 · "A role outside the three is refused": nothing is created; the reason is on the field, under the module's validation
    // code. The names are matched ordinally — «Admin» is not «admin».
    [Theory]
    [InlineData("owner")]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Add_ARoleOutsideTheThree_Returns400RoleInvalid_NothingCreated(string? role)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Роль", "Неверная");

        var answer = await AddAsync(organization.Id, new { userId = user, role }, admin.Token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(ValidationFailed, answer.ErrorCode);
        Assert.Equal("organizations.membership.role_invalid", FieldReason(answer, "role"));
        Assert.False(answer.Body.GetProperty("errors").TryGetProperty("userId", out _));
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, user));
    }

    // api-contract § 1.4 · "every field reason at once": a missing, blank or over-long user id and a bad role are both reported.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Add_AnInvalidUserId_AndABadRole_BothReasonsAtOnce(string? userId)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);

        var answer = await AddAsync(organization.Id, new { userId, role = "boss" }, admin.Token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(ValidationFailed, answer.ErrorCode);
        Assert.Equal("organizations.membership.user_invalid", FieldReason(answer, "userId"));
        Assert.Equal("organizations.membership.role_invalid", FieldReason(answer, "role"));
    }

    // api-contract § 0.3 · a user id is 1 to 255 characters: 256 is a 400; an empty body or one with no fields too.
    [Fact]
    public async Task Add_AUserIdOf256Characters_AndAnEmptyBody_AreRefused()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);

        var tooLong = await AddAsync(organization.Id, new { userId = new string('u', 256), role = "member" }, admin.Token);
        var empty = await AddAsync(organization.Id, new { }, admin.Token);

        Assert.Equal((HttpStatusCode.BadRequest, ValidationFailed), (tooLong.Status, tooLong.ErrorCode));
        Assert.Equal("organizations.membership.user_invalid", FieldReason(tooLong, "userId"));
        Assert.False(tooLong.Body.GetProperty("errors").TryGetProperty("role", out _));
        Assert.Equal((HttpStatusCode.BadRequest, ValidationFailed), (empty.Status, empty.ErrorCode));
        Assert.Equal("organizations.membership.user_invalid", FieldReason(empty, "userId"));
        Assert.Equal("organizations.membership.role_invalid", FieldReason(empty, "role"));
    }

    // US-1 · "An inactive or unknown user is neither offered nor added" (BR-M1-32): a disabled account and an id the projection
    // does not hold answer 409 «Этого пользователя нельзя добавить в организацию», and nothing is created.
    [Fact]
    public async Task Add_AnInactiveAndAnUnknownUser_Return409UserUnavailable_NothingCreated()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);
        var disabled = await WorkItemsTestData.AddUserAsync(factory, "Пётр", "Отключённый", active: false);
        var unknown = MembershipTestData.NewUserId();

        foreach (var userId in new[] { disabled, unknown })
        {
            var answer = await AddAsync(organization.Id, new { userId, role = "member" }, admin.Token);

            Assert.Equal(HttpStatusCode.Conflict, answer.Status);
            Assert.Equal("organizations.membership.user_unavailable", answer.ErrorCode);
            Assert.Equal("Этого пользователя нельзя добавить в организацию", answer.Detail);
            Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, userId));
        }
    }

    // US-1 / doc-006 US-3 scenario 4 · "a user who already is a member": 409 «Пользователь уже состоит в организации»; the stored
    // membership keeps its role and its «Добавлен».
    [Fact]
    public async Task Add_AnExistingMember_Returns409AlreadyMember_TheMembershipIsUntouched()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Уже");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, user, OrganizationRole.Viewer);

        var answer = await AddAsync(organization.Id, new { userId = user, role = "admin" }, admin.Token);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.membership.already_member", answer.ErrorCode);
        var stored = await MembershipTestData.FindMembershipAsync(factory, organization.Id, user);
        Assert.Equal((OrganizationRole.Viewer, OrganizationsTestData.Creator), (stored!.Role, stored.AddedBy));
    }

    // api-contract § 1.4 · "user_unavailable is answered before already_member": a member whose account is disabled, added again.
    [Fact]
    public async Task Add_AnInactiveExistingMember_AnswersUserUnavailable_BeforeAlreadyMember()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Олег", "Отключённый", active: false);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, user);

        var answer = await AddAsync(organization.Id, new { userId = user, role = "member" }, admin.Token);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.membership.user_unavailable", answer.ErrorCode);
    }

    // doc-006 US-3 scenario 3 · a user may belong to several organizations: a member of another organization is added here, and
    // the other membership is unchanged.
    [Fact]
    public async Task Add_AMemberOfAnotherOrganization_IsAdded_TheOtherMembershipIsUnchanged()
    {
        var here = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var there = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(here.Id);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Двойная");
        await MembershipTestData.AddMemberAsync(factory, there.Id, user, OrganizationRole.Viewer);

        var answer = await AddAsync(here.Id, new { userId = user, role = "admin" }, admin.Token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(OrganizationRole.Viewer, (await MembershipTestData.FindMembershipAsync(factory, there.Id, user))!.Role);
        Assert.Equal(OrganizationRole.Admin, (await MembershipTestData.FindMembershipAsync(factory, here.Id, user))!.Role);
    }

    // BR-M1-25 / mod-014 BR-UP-17 · an administrator the projection does not know yet (a creator) adds as usual, and is shown as
    // «Неизвестный пользователь» in «Добавлен».
    [Fact]
    public async Task Add_ByAnAdministratorTheProjectionDoesNotKnow_Works_AndShowsThemUnknown()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await AdminAsync(organization.Id);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Добавленная");

        var answer = await AddAsync(organization.Id, new { userId = user, role = "member" }, admin.Token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var addedBy = answer.Body.GetProperty("addedBy");
        Assert.Equal((admin.Id, "unknown"), (addedBy.GetProperty("id").GetString(), addedBy.GetProperty("state").GetString()));
        Assert.Equal(JsonValueKind.Null, addedBy.GetProperty("displayName").ValueKind);
        Assert.Equal(JsonValueKind.Null, addedBy.GetProperty("email").ValueKind);
    }

    // NFR Concurrency / BR-M1-5 · two administrators add the same user at the same moment: exactly one 201, the other 409
    // «уже состоит», one membership. (The primary key closes it; the loser of the race gets the pre-check's code.)
    [Fact]
    public async Task Add_TheSameUserTwiceAtTheSameMoment_OneCreated_OneAlreadyMember()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var first = await AdminAsync(organization.Id);
        var second = await AdminAsync(organization.Id);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Гонка");
        var body = new { userId = user, role = "member" };

        var answers = await Task.WhenAll(AddAsync(organization.Id, body, first.Token), AddAsync(organization.Id, body, second.Token));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], answers.Select(answer => answer.Status).Order());
        Assert.Equal("organizations.membership.already_member", answers.Single(answer => answer.Status == HttpStatusCode.Conflict).ErrorCode);
        Assert.NotNull(await MembershipTestData.FindMembershipAsync(factory, organization.Id, user));
    }

    // US-2 · "A member whose role lacks the code is refused": a member and a viewer get 403 «Недостаточно прав», nothing is created
    // — even with a body that would be refused (the 403 comes first).
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Add_MemberAndViewer_Get403_NothingCreated(OrganizationRole role)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Закрытая");

        var valid = await AddAsync(organization.Id, new { userId = user, role = "member" }, caller.Token);
        var invalid = await AddAsync(organization.Id, new { userId = "", role = "boss" }, caller.Token);

        foreach (var answer in new[] { valid, invalid })
        {
            Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
            Assert.Equal("organizations.access.forbidden", answer.ErrorCode);
        }

        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, user));
    }

    // US-2 · "A non-member gets «Организация не найдена»" at the add too, and 401 without a token.
    [Fact]
    public async Task Add_NonMember_Is404_NoToken_Is401_NothingCreated()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var user = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Чужая");
        var body = new { userId = user, role = "member" };

        var nonMember = await AddAsync(organization.Id, body, MembershipTestData.Outsider());
        var anonymous = await AddAsync(organization.Id, body, token: null);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, user));
    }
}
