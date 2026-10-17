using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/members</c> (mod-016 api-contract § 1.2; US-1 "An inactive or unknown member is
/// shown as such", "Past 500 ids the list and the member searches still answer", doc-006 US-3 scenarios 7, 14 and 15): every
/// member, with who added them and who last changed their role, in the order of § 0.5, from the user projection and never from
/// Keycloak — which these tests do not even have.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetMembersEndpointTests(MustaskApiFactory factory)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private Task<Answer> GetAsync(Guid organizationId, string token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"/api/organizations/{organizationId}/members", token: token);

    private static List<JsonElement> Items(Answer answer) => answer.Body.EnumerateArray().ToList();

    private static string? Name(JsonElement reference) =>
        reference.GetProperty("displayName").ValueKind == JsonValueKind.Null ? null : reference.GetProperty("displayName").GetString();

    // api-contract § 1.2: the shape of a Member and of a UserRef — nothing else about a person is offered.
    [Fact]
    public async Task Get_AMember_HasTheContractsShape()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var reader = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var answer = await GetAsync(organization.Id, reader.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        var member = Items(answer).Single(item => item.GetProperty("user").GetProperty("id").GetString() == reader.Id);
        Assert.Equal(["addedAt", "addedBy", "role", "roleChangedAt", "roleChangedBy", "user"], member.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["displayName", "email", "id", "state"], member.GetProperty("user").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["displayName", "email", "id", "state"], member.GetProperty("addedBy").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("viewer", member.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, member.GetProperty("roleChangedBy").ValueKind);
        Assert.Equal(JsonValueKind.Null, member.GetProperty("roleChangedAt").ValueKind);
    }

    // doc-006 US-3 scenarios 7 and 15 · "who added them and when": the list names the adder, shown as a person, and every role reads.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Get_EveryRoleReads_AndSeesWhoAddedEachMember(OrganizationRole role)
    {
        var tag = Tag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var adder = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Admin, $"Анна{tag}", "Админова");
        var reader = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);
        var added = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, added, $"Вера{tag}", "Новикова", "vera@example.org");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, added, OrganizationRole.Member, addedBy: adder.Id);

        var answer = await GetAsync(organization.Id, reader.Token);

        var member = Items(answer).Single(item => item.GetProperty("user").GetProperty("id").GetString() == added);
        Assert.Equal($"Вера{tag} Новикова", Name(member.GetProperty("user")));
        Assert.Equal("vera@example.org", member.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal("active", member.GetProperty("user").GetProperty("state").GetString());
        Assert.Equal(($"Анна{tag} Админова", adder.Id), (Name(member.GetProperty("addedBy")), member.GetProperty("addedBy").GetProperty("id").GetString()));
        Assert.Equal(OrganizationsTestData.Now, member.GetProperty("addedAt").GetDateTimeOffset());
    }

    // api-contract § 0.5: ordered by display name (ru, primary strength: «ё» with «е», case ignored), then by user id.
    [Fact]
    public async Task Get_OrdersByName_ThenById_UnknownUsersLastById()
    {
        var tag = Tag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var ids = new Dictionary<string, string>();

        foreach (var (key, first, last) in new[] { ("b", "Борис", "Борисов"), ("yo", "ёлка", "Новикова"), ("e", "Елена", "Яблокова"), ("a2", "анна", "Иванова"), ("a1", "Анна", "Иванова") })
        {
            ids[key] = $"{key}-{tag}-{Guid.NewGuid():N}"[..40];
            await MembershipTestData.ProjectUserAsync(factory, ids[key], first + tag, last);
            await MembershipTestData.AddMemberAsync(factory, organization.Id, ids[key]);
        }

        ids["z"] = $"z-ghost-{tag}";
        ids["g"] = $"a-ghost-{tag}";
        await MembershipTestData.AddMemberAsync(factory, organization.Id, ids["z"]);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, ids["g"]);
        var reader = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var members = Items(await GetAsync(organization.Id, reader.Token))
            .Select(item => item.GetProperty("user"))
            .Where(user => ids.ContainsValue(user.GetProperty("id").GetString()!))
            .ToList();

        // «Анна…» ties by name (case ignored) and goes by id; «Борис…», «Елена…», «ёлка…» (ё = е): Елена < ёлка by the next letters.
        var order = members.Select(user => user.GetProperty("id").GetString()).ToList();
        Assert.Equal(
            new[] { ids["a1"], ids["a2"], ids["b"], ids["yo"], ids["e"], ids["g"], ids["z"] }.Length,
            order.Count);
        Assert.Equal([ids["g"], ids["z"]], order.TakeLast(2));
        Assert.Equal([ids["a1"], ids["a2"], ids["b"]], order.Take(3));
        Assert.Equal("unknown", members[^1].GetProperty("state").GetString());
    }

    // US-1 · "An inactive or unknown member is shown as such": «неактивен» (the state), a member the projection does not know is
    // «Неизвестный пользователь» (no name, no e-mail), after every member with a name; both keep their role and their team membership.
    [Fact]
    public async Task Get_InactiveAndUnknownMembers_AreListedWithTheirState_KeepTheirRoleAndTeams()
    {
        var tag = Tag();
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var disabled = MembershipTestData.NewUserId();
        var ghost = $"ghost-{tag}";
        await MembershipTestData.ProjectUserAsync(factory, disabled, $"Олег{tag}", "Иванов", "oleg@example.org", active: false);
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, disabled, OrganizationRole.Viewer);
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, ghost, OrganizationRole.Admin);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.Platform.Id, disabled);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, structure.Platform.Id, ghost);
        var reader = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);

        var members = Items(await GetAsync(structure.OrganizationId, reader.Token));

        var inactive = members.Single(item => item.GetProperty("user").GetProperty("id").GetString() == disabled);
        Assert.Equal(("inactive", $"Олег{tag} Иванов", "oleg@example.org", "viewer"), (
            inactive.GetProperty("user").GetProperty("state").GetString(),
            Name(inactive.GetProperty("user")),
            inactive.GetProperty("user").GetProperty("email").GetString(),
            inactive.GetProperty("role").GetString()));

        var unknown = members.Single(item => item.GetProperty("user").GetProperty("id").GetString() == ghost);
        Assert.Equal(("unknown", "admin"), (unknown.GetProperty("user").GetProperty("state").GetString(), unknown.GetProperty("role").GetString()));
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("user").GetProperty("displayName").ValueKind);
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("user").GetProperty("email").ValueKind);

        // After every member with a name, whichever the projection knows.
        var positions = members.Select((item, index) => (Id: item.GetProperty("user").GetProperty("id").GetString(), Index: index)).ToDictionary(pair => pair.Id!, pair => pair.Index);
        Assert.True(positions[ghost] > positions[disabled]);
        Assert.True(positions[ghost] > positions[reader.Id] || Name(members[positions[reader.Id]].GetProperty("user")) is null);

        // They keep their team memberships (BR-M1-17).
        Assert.Equal(2, (await MembershipTestData.TeamMemberIdsAsync(factory, structure.Platform.Id)).Count);
    }

    // doc-006 US-3 scenario 14 · "a re-enabled account loses «неактивен»": a projection update flips the state back.
    [Fact]
    public async Task Get_AReEnabledAccount_IsActiveAgain()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var disabled = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, disabled, "Олег", "Иванов", active: false);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, disabled);
        var reader = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var before = Items(await GetAsync(organization.Id, reader.Token)).Single(item => item.GetProperty("user").GetProperty("id").GetString() == disabled);
        await WorkItems.WorkItemsTestData.ExecuteAsync(factory, $"UPDATE directory_users SET enabled = true, is_active = true WHERE id = {disabled}");
        var after = Items(await GetAsync(organization.Id, reader.Token)).Single(item => item.GetProperty("user").GetProperty("id").GetString() == disabled);

        Assert.Equal("inactive", before.GetProperty("user").GetProperty("state").GetString());
        Assert.Equal("active", after.GetProperty("user").GetProperty("state").GetString());
    }

    // The list is the organization's own members only.
    [Fact]
    public async Task Get_ListsOnlyThisOrganizationsMembers()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var elsewhere = await MembershipTestData.NewMemberAsync(factory, other.Id, OrganizationRole.Admin);
        var reader = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var members = Items(await GetAsync(organization.Id, reader.Token));

        Assert.DoesNotContain(members, item => item.GetProperty("user").GetProperty("id").GetString() == elsewhere.Id);
        Assert.Contains(members, item => item.GetProperty("user").GetProperty("id").GetString() == reader.Id);
    }

    // US-1 · "Past 500 ids the list and the member searches still answer": 501 members, some of whose «Добавлен» name users who
    // are no longer members — all 501 are listed, each «Добавлен» with a name; the answer is never refused for its size.
    [Fact]
    public async Task Get_501Members_AllListed_EachAddedByShowsAName()
    {
        var tag = Tag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var pushes = Enumerable.Range(0, 501).Select(i => UserDirectoryTestData.Push($"m{i:D3}-{tag}", $"Участник{tag}", $"{i:D3}", null)).ToList();
        pushes.Add(UserDirectoryTestData.Push($"former-{tag}", "Бывший", "Админ", null));
        await UserDirectoryTestData.SeedAsync(factory, pushes);
        await WorkItems.WorkItemsTestData.ExecuteAsync(
            factory,
            $"INSERT INTO organization_memberships (organization_id, user_id, role, added_by, added_at) SELECT {organization.Id}, 'm' || lpad(i::text, 3, '0') || '-' || {tag}, 'member', 'former-' || {tag}, now() FROM generate_series(0, 500) i");
        var reader = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var answer = await GetAsync(organization.Id, reader.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var members = Items(answer);
        Assert.Equal(502, members.Count);
        var listed = members.Where(item => item.GetProperty("user").GetProperty("id").GetString()!.StartsWith('m')).ToList();
        Assert.Equal(501, listed.Count);
        Assert.All(listed, item => Assert.Equal("Бывший Админ", Name(item.GetProperty("addedBy"))));
        Assert.All(listed, item => Assert.NotNull(Name(item.GetProperty("user"))));
    }

    [Fact]
    public async Task Get_NonMember_Is404_NoToken_Is401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var nonMember = await GetAsync(organization.Id, MembershipTestData.Outsider());
        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"/api/organizations/{organization.Id}/members");

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
    }
}
