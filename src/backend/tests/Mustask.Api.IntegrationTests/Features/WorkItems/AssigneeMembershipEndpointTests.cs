using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-016 US-5 "Only an active organization member can be made an assignee" (BR-M1-26, status.md D-4), through the endpoints:
/// the «Исполнитель» picker offers only active members of the item's organization, a save refuses to <i>set</i> anyone else, an
/// assignee who leaves stays and never refuses a save of other fields, and every read that carries an assignee says whether
/// they are still a member. The «неактивен» and «не в организации» marks are feat-017's.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AssigneeMembershipEndpointTests(MustaskApiFactory factory)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private Task<Answer> SearchAsync(Guid organizationId, string text) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.Candidates(organizationId, text), token: OrganizationsTokens.SignedIn());

    private static List<string?> Names(Answer answer) =>
        answer.Body.GetProperty("users").EnumerateArray().Select(user => user.GetProperty("displayName").GetString()).ToList();

    private static string? Text(JsonElement item, params string[] path)
    {
        foreach (var name in path)
        {
            item = item.GetProperty(name);
        }

        return item.ValueKind == JsonValueKind.Null ? null : item.GetString();
    }

    // US-5 · "The picker offers only active members of the item's organization": «Анна» (active member) is offered; «Олег» (member
    // whose account is disabled) and «Мария» (an active projected user who is a member only of B) are not; typing «Мария» offers nobody.
    [Fact]
    public async Task Candidates_OnlyActiveMembersOfTheItemsOrganization_AreOffered()
    {
        var tag = Tag();
        var a = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var b = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var anna = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Смирнова");
        var oleg = await WorkItemsTestData.AddUserAsync(factory, $"Олег{tag}", "Смирнов", active: false);
        var maria = await WorkItemsTestData.AddUserAsync(factory, $"Мария{tag}", "Смирнова");
        await WorkItemsTestData.AddUserAsync(factory, $"Никита{tag}", "Смирнов");
        await MembershipTestData.AddMemberAsync(factory, a.Id, anna);
        await MembershipTestData.AddMemberAsync(factory, a.Id, oleg);
        await MembershipTestData.AddMemberAsync(factory, b.Id, maria);

        var offered = await SearchAsync(a.Id, "смирнов");
        var typedMaria = await SearchAsync(a.Id, $"Мария{tag}");
        var typedNobody = await SearchAsync(a.Id, $"Никита{tag}");
        var inB = await SearchAsync(b.Id, "смирнов");

        Assert.Equal(HttpStatusCode.OK, offered.Status);
        Assert.Equal([$"Анна{tag} Смирнова"], Names(offered));
        Assert.Empty(Names(typedMaria));
        Assert.Empty(Names(typedNobody));
        Assert.Equal([$"Мария{tag} Смирнова"], Names(inB));
    }

    // US-5 · "A member re-added to the organization can be assigned again": any role may be an assignee.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Candidates_AMemberOfAnyRole_IsOffered_AndAReAddedMemberAgain(OrganizationRole role)
    {
        var tag = Tag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var anna = await WorkItemsTestData.AddUserAsync(factory, $"Анна{tag}", "Смирнова");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, anna);
        await MembershipTestData.RemoveMemberAsync(factory, organization.Id, anna);

        Assert.Empty(Names(await SearchAsync(organization.Id, $"Анна{tag}")));

        await MembershipTestData.AddMemberAsync(factory, organization.Id, anna, role);

        Assert.Equal([$"Анна{tag} Смирнова"], Names(await SearchAsync(organization.Id, $"Анна{tag}")));
    }

    // US-5 · "A non-member, inactive or unknown assignee is refused": the save answers 409 «Этого пользователя нельзя назначить
    // исполнителем.» and the assignee is unchanged.
    [Fact]
    public async Task Save_ANonMemberAnInactiveMemberAndAnUnknownId_AreRefused_AssigneeUnchanged()
    {
        var tag = Tag();
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение");
        var maria = await WorkItemsTestData.AddUserAsync(factory, $"Мария{tag}", "Соколова");
        var oleg = await WorkItemsTestData.AddMemberAsync(factory, world, $"Олег{tag}", "Иванов", active: false);

        foreach (var userId in new[] { maria, oleg, Guid.NewGuid().ToString() })
        {
            var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Иное", assignee = new { userId } });

            Assert.Equal(HttpStatusCode.Conflict, answer.Status);
            Assert.Equal("workitems.workitem.assignee_unavailable", answer.ErrorCode);
            Assert.Equal("Этого пользователя нельзя назначить исполнителем.", answer.Detail);
        }

        var stored = (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0];
        Assert.Equal(("Падение", (string?)null), (stored.Title, stored.AssigneeId));
    }

    // BE-TES-2 · "...or unknown": the unknown refusal stands on its own. A MEMBER the projection has never received (a creator
    // BR-M1-25 stored whatever the projection says) is a member, yet cannot be newly made an assignee — api-contract § 3.4 refuses
    // "one the projection does not know" — while the same id, once the projection receives and activates them, can. A refusal on
    // membership alone would pass every other test here, so this one fails if the directory check were dropped.
    [Fact]
    public async Task Save_AMemberTheProjectionDoesNotKnow_IsRefused_UntilTheProjectionReceivesThem()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение");
        var ghost = MembershipTestData.NewUserId();
        await MembershipTestData.AddMemberAsync(factory, world.OrganizationId, ghost);

        var refused = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Иное", assignee = new { userId = ghost } });

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal("workitems.workitem.assignee_unavailable", refused.ErrorCode);
        var stored = (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0];
        Assert.Equal(("Падение", (string?)null), (stored.Title, stored.AssigneeId));

        await MembershipTestData.ProjectUserAsync(factory, ghost, "Анна", "Смирнова");

        var accepted = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = ghost } });

        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(ghost, Text(accepted.Body, "assignee", "id"));
    }

    // A member of ANOTHER organization is not one of this item's organization.
    [Fact]
    public async Task Save_AMemberOfAnotherOrganizationOnly_IsRefused()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение");
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var maria = await WorkItemsTestData.AddUserAsync(factory, "Мария", "Соколова");
        await MembershipTestData.AddMemberAsync(factory, other.Id, maria);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = maria } });

        Assert.Equal("workitems.workitem.assignee_unavailable", answer.ErrorCode);
    }

    // US-5 · "An assignee who leaves the organization stays": the item keeps her, saving its title or its state succeeds, and the
    // form shows her; "Clearing the assignee is always allowed".
    [Fact]
    public async Task Save_AnAssigneeWhoLeft_Stays_OtherFieldsSave_ClearingIsAllowed()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение");
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = anna } });

        await MembershipTestData.RemoveMemberAsync(factory, world.OrganizationId, anna);

        var retitled = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Новое название" });
        var restated = await WorkItemsTestData.SaveAsync(factory, world, bug, new { stateId = world.Types["BUG"].StateId("Активна").ToString() });
        var resent = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Ещё", assignee = new { userId = anna } });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), (retitled.Status, restated.Status, resent.Status));
        Assert.Equal(anna, Text(retitled.Body, "assignee", "id"));
        Assert.Equal(anna, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0].AssigneeId);

        var cleared = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = (string?)null } });

        Assert.Equal(HttpStatusCode.OK, cleared.Status);
        Assert.Equal(JsonValueKind.Null, cleared.Body.GetProperty("assignee").ValueKind);
    }

    // US-5 · "A member re-added to the organization can be assigned again": removed, then added back as viewer.
    [Fact]
    public async Task Save_AReAddedMember_AsViewer_CanBeAssigned()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение");
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова");
        await MembershipTestData.RemoveMemberAsync(factory, world.OrganizationId, anna);

        var refused = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = anna } });
        await MembershipTestData.AddMemberAsync(factory, world.OrganizationId, anna, OrganizationRole.Viewer);
        var accepted = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = anna } });

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
    }

    // US-5 · "The reads say whether the assignee is still a member": the item (read and save answer), and the list.
    [Fact]
    public async Task Reads_ItemAndList_SayWhetherTheAssigneeIsStillAMember()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var stays = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Остаётся");
        var left = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Ушла");
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова");
        var oleg = await WorkItemsTestData.AddMemberAsync(factory, world, "Олег", "Иванов", role: OrganizationRole.Viewer);
        var saved = await WorkItemsTestData.SaveAsync(factory, world, stays, new { assignee = new { userId = oleg } });
        await WorkItemsTestData.SaveAsync(factory, world, left, new { assignee = new { userId = anna } });
        await MembershipTestData.RemoveMemberAsync(factory, world.OrganizationId, anna);

        var staysItem = await WorkItemsTestData.ReadAsync(factory, world, stays);
        var leftItem = await WorkItemsTestData.ReadAsync(factory, world, left);
        var list = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data").EnumerateArray().ToList();

        Assert.True(saved.Body.GetProperty("assignee").GetProperty("isMember").GetBoolean());
        Assert.True(staysItem.GetProperty("assignee").GetProperty("isMember").GetBoolean());
        Assert.False(leftItem.GetProperty("assignee").GetProperty("isMember").GetBoolean());
        Assert.Equal(
            [(stays, true), (left, false)],
            list.OrderBy(row => row.GetProperty("number").GetInt32()).Select(row => (row.GetProperty("identifier").GetString()!, row.GetProperty("assignee").GetProperty("isMember").GetBoolean())));

        // The ex-member keeps their name: only the flag differs ("no mark until feat-017").
        Assert.Equal("Анна Смирнова", Text(leftItem, "assignee", "displayName"));
    }

    // US-5 · "The reads say whether the assignee is still a member": the team's backlog and board.
    [Fact]
    public async Task Reads_BacklogAndBoard_SayWhetherTheAssigneeIsStillAMember()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var stays = await BoardsTestData.CreateAsync(factory, world, "US", "Остаётся", world.PlatformAreaId);
        var left = await BoardsTestData.CreateAsync(factory, world, "US", "Ушла", world.PlatformAreaId);
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Смирнова");
        var oleg = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Олег", "Иванов");
        await WorkItemsTestData.SaveAsync(factory, world.Items, stays.Identifier, new { assignee = new { userId = oleg } });
        await WorkItemsTestData.SaveAsync(factory, world.Items, left.Identifier, new { assignee = new { userId = anna } });
        await MembershipTestData.RemoveMemberAsync(factory, world.Items.OrganizationId, anna);

        var backlog = (await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId)).Body.GetProperty("items").EnumerateArray().ToList();
        var board = (await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId)).Body;
        var cards = BoardsTestData.Column(board, "Новая").GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(
            [(stays.Identifier, true), (left.Identifier, false)],
            backlog.OrderBy(row => row.GetProperty("order").GetInt32()).Select(row => (row.GetProperty("identifier").GetString()!, row.GetProperty("assignee").GetProperty("isMember").GetBoolean())));
        Assert.Equal(
            [(stays.Identifier, true), (left.Identifier, false)],
            cards.Select(card => (card.GetProperty("identifier").GetString()!, card.GetProperty("assignee").GetProperty("isMember").GetBoolean())));
    }

    // api-contract § 3.3: an item with no assignee has none (the field stays null, no flag appears).
    [Fact]
    public async Task Reads_NoAssignee_StaysNull()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Без исполнителя");

        var item = await WorkItemsTestData.ReadAsync(factory, world, bug);

        Assert.Equal(JsonValueKind.Null, item.GetProperty("assignee").ValueKind);
    }

    // A member the projection does not know is still a member: the flag reads the membership alone.
    [Fact]
    public async Task Reads_AMemberTheProjectionDoesNotKnow_IsAMember()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение");
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = anna } });
        await WorkItemsTestData.ExecuteAsync(factory, $"DELETE FROM directory_users WHERE id = {anna}");

        var item = await WorkItemsTestData.ReadAsync(factory, world, bug);

        Assert.True(item.GetProperty("assignee").GetProperty("isMember").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("assignee").GetProperty("displayName").ValueKind);
    }
}
