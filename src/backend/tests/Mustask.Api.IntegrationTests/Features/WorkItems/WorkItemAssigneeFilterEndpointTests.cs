using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.WorkItems.SearchTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// The «Исполнитель» filter and its options (feat-003 US-2, BR-WS-6..-8, -10, -15; api-contract § 2.1, § 2.2, § 3.2), through the
/// endpoints and a real PostgreSQL, over the spec's world (<see cref="SearchTestData"/>): Mia holds BUG-42 and US-1, Ivan — who has
/// left — BUG-2 and the deleted US-5, Petr — whose account is disabled — TASK-4, Zoya only the deleted FEAT-6, and Ann and Rita nothing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemAssigneeFilterEndpointTests(MustaskApiFactory factory)
{
    private async Task<string[]> RowsAsync(SearchWorld data, IEnumerable<string>? states = null, IEnumerable<string>? assignees = null, IEnumerable<string>? teams = null)
    {
        var answer = await ListAsync(factory, data.World, Query(states: states, assignees: assignees, teams: teams));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(answer.Body.GetProperty("data").GetArrayLength(), Count(answer));

        return Identifiers(answer);
    }

    private async Task<List<(string Id, string? Name, string? Email, bool Active, bool Member)>> OptionsOfAsync(SearchWorld data, params string[] states)
    {
        var answer = await OptionsAsync(factory, data.World, Query(states: states));

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return Options(answer);
    }

    // ---- The options -------------------------------------------------------------------------------------------------------------

    // US-2 "The assignee field offers the people assigned to the project's live items": Ivan, Mia, Petr by display name; not Ann or
    // Rita (assigned nothing), not Zoya (only a deleted item), not Oleg (another organization).
    [Fact]
    public async Task Options_ThePeopleAssignedToLiveItems_ByDisplayName_MembersFormerMembersAndDisabledAlike()
    {
        var data = await CreateAsync(factory);

        var options = await OptionsOfAsync(data);

        Assert.Equal(
            [
                (data.People.Ivan, "Иван Котов", "ivan@alfa.test", true, false),
                (data.People.Mia, "Мия Петрова", "mia@alfa.test", true, true),
                (data.People.Petr, "Пётр Ли", "petr@alfa.test", false, true),
            ],
            options);
        Assert.DoesNotContain(options, option => option.Id == data.People.Ann || option.Id == data.People.Rita || option.Id == data.People.Zoya);
    }

    // US-2 "With «Удалена» chosen, the people of deleted items are offered too": Zoya joins, in the one order.
    [Fact]
    public async Task Options_WhileUdalenaIsChosen_ThePeopleOfDeletedItemsJoin()
    {
        var data = await CreateAsync(factory);

        var options = await OptionsOfAsync(data, "Удалена");

        Assert.Equal(
            [data.People.Zoya, data.People.Ivan, data.People.Mia, data.People.Petr],
            options.Select(option => option.Id));
        Assert.Equal(["Зоя Ким", "Иван Котов", "Мия Петрова", "Пётр Ли"], options.Select(option => option.Name));
    }

    // BR-WS-6 · only whether a chosen name reaches the bin matters: other states change nothing, and neither does a name nothing bears.
    [Theory]
    [InlineData("Новая")]
    [InlineData("Активна")]
    [InlineData("Разрешена")]
    [InlineData("Закрыта")]
    [InlineData("Готово")]
    [InlineData("удалена")]
    [InlineData("")]
    [InlineData("%00")]
    public async Task Options_AStateThatDoesNotReachTheBin_ChangesNothing(string name)
    {
        var data = await CreateAsync(factory);

        // "%00" stands for the escaped U+0000 itself; every other name is escaped here.
        var value = name == "%00" ? name : Uri.EscapeDataString(name);
        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, OptionsPath(data.World) + "?state=" + value, token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([data.People.Ivan, data.People.Mia, data.People.Petr], Options(answer).Select(option => option.Id));
    }

    // BR-WS-6 · "Удалена" beside other states: still the deleted items' people, and Zoya once.
    [Fact]
    public async Task Options_UdalenaBesideOtherStates_IsTheSameAsUdalenaAlone()
    {
        var data = await CreateAsync(factory);

        var alone = await OptionsOfAsync(data, "Удалена");
        var beside = await OptionsOfAsync(data, "Новая", "Удалена", "Активна");

        Assert.Equal(alone, beside);
    }

    // BR-WS-7 · "A person the directory does not know reads without «(неактивен)»": no name, no e-mail, not active, member as the
    // membership says; users the directory does not know come after every named person.
    [Fact]
    public async Task Options_APersonTheDirectoryDoesNotKnow_HasNoNameAndNoEmail_AndComesLast()
    {
        var data = await CreateAsync(factory);
        await WorkItemsTestData.ExecuteAsync(factory, $"DELETE FROM directory_users WHERE id = {data.People.Petr}");

        var options = await OptionsOfAsync(data);

        Assert.Equal(
            [
                (data.People.Ivan, "Иван Котов", "ivan@alfa.test", true, false),
                (data.People.Mia, "Мия Петрова", "mia@alfa.test", true, true),
                (data.People.Petr, (string?)null, (string?)null, false, true),
            ],
            options);

        // His membership ends too: still no name, and now not a member.
        await MembershipTestData.RemoveMemberAsync(factory, data.World.OrganizationId, data.People.Petr);

        Assert.Equal((data.People.Petr, (string?)null, (string?)null, false, false), (await OptionsOfAsync(data))[^1]);
    }

    // BR-WS-6 · two users the directory does not know are two options, by id, after every named person.
    [Fact]
    public async Task Options_TwoUnknownUsers_AreTwoOptions_OrderedById_AfterTheNamedOnes()
    {
        var data = await CreateAsync(factory);
        await WorkItemsTestData.ExecuteAsync(factory, $"DELETE FROM directory_users WHERE id = {data.People.Petr}");
        await WorkItemsTestData.ExecuteAsync(factory, $"DELETE FROM directory_users WHERE id = {data.People.Ivan}");

        var options = await OptionsOfAsync(data);

        Assert.Equal([data.People.Mia, .. new[] { data.People.Ivan, data.People.Petr }.Order(StringComparer.Ordinal)], options.Select(option => option.Id));
        Assert.All(options.Skip(1), option => Assert.Null(option.Name));
    }

    // BR-WS-6 · people of equal display names are two options, by id.
    [Fact]
    public async Task Options_EqualDisplayNames_AreTwoOptions_ByIdInCodePointOrder()
    {
        var data = await CreateAsync(factory);
        var twin = await WorkItemsTestData.AddMemberAsync(factory, data.World, "Мия", "Петрова", "mia.twin@alfa.test");
        var item = await CreateItemAsync(factory, data.Teams, "TASK", "Двойник", null);
        await WorkItemsTestData.SaveAsync(factory, data.World, item.Identifier, new { assignee = new { userId = twin } });

        var options = await OptionsOfAsync(data);

        var twins = options.Where(option => option.Name == "Мия Петрова").Select(option => option.Id).ToList();
        Assert.Equal(new[] { data.People.Mia, twin }.Order(StringComparer.Ordinal), twins);
        Assert.Equal(4, options.Count);
    }

    // US-2 "The marks follow the membership at each read": Ivan added back is a member again.
    [Fact]
    public async Task Options_AFormerMemberAddedBack_IsAMemberAgain()
    {
        var data = await CreateAsync(factory);
        Assert.False((await OptionsOfAsync(data)).Single(option => option.Id == data.People.Ivan).Member);

        await MembershipTestData.AddMemberAsync(factory, data.World.OrganizationId, data.People.Ivan);

        Assert.True((await OptionsOfAsync(data)).Single(option => option.Id == data.People.Ivan).Member);
    }

    // BR-WS-6 · "nobody else: ... no assignee of another project": the options name only the people of this project's items.
    [Fact]
    public async Task Options_AreOnlyThisProjectsAssignees()
    {
        var data = await CreateAsync(factory);
        var other = data.World with { ProjectId = data.Teams.OtherProjectId, Key = "BET" };
        var outsider = await WorkItemsTestData.AddMemberAsync(factory, data.World, "Ольга", "Другая");
        var foreign = await WorkItemsTestData.CreateItemAsync(factory, other, "TASK", "Чужая задача");
        await WorkItemsTestData.SaveAsync(factory, other, foreign, new { assignee = new { userId = outsider } });

        Assert.DoesNotContain(await OptionsOfAsync(data), option => option.Id == outsider);
        Assert.DoesNotContain(await OptionsOfAsync(data, "Удалена"), option => option.Id == outsider);
        Assert.Equal([outsider], Options(await OptionsAsync(factory, other)).Select(option => option.Id));
    }

    // A project nobody is assigned in answers an empty list, not an error.
    [Fact]
    public async Task Options_NobodyAssigned_AnEmptyList()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Без исполнителя");

        var answer = await OptionsAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);
        Assert.Equal(0, answer.Body.GetArrayLength());
    }

    // US-2 "A reader filters by assignee": the read code is every role's.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Options_EveryRole_IsOfferedTheSamePeople(OrganizationRole role)
    {
        var data = await CreateAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, data.World.OrganizationId, role);

        var answer = await OptionsAsync(factory, data.World, token: member.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([data.People.Ivan, data.People.Mia, data.People.Petr], Options(answer).Select(option => option.Id));
    }

    // US-2 "The options' read is contained like the list's".
    [Fact]
    public async Task Options_ANonMember_GetsTheOrganizationsNotFound_AMalformedOneToo()
    {
        var data = await CreateAsync(factory);

        var stranger = await OptionsAsync(factory, data.World, token: MembershipTestData.Outsider());
        var malformed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/not-a-guid/projects/{data.World.ProjectId}/workitems/assignee-options", token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.NotFound, stranger.Status);
        Assert.Equal("organizations.organization.not_found", stranger.ErrorCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.Status);
        Assert.Equal("organizations.organization.not_found", malformed.ErrorCode);
    }

    [Fact]
    public async Task Options_AForeignDeactivatedUnknownOrMalformedProject_GetsTheProjectsNotFound()
    {
        var data = await CreateAsync(factory);
        var foreign = await WorkItemsTestData.CreateWorldAsync(factory, "OTH", "Чужой");
        var sleeping = await OrganizationsTestData.CreateProjectAsync(
            factory, data.World.OrganizationId, "Спящий-2", "SLQ", Mustask.Domain.Organizations.ProjectStatus.Deactivated);
        var organization = data.World.OrganizationId;

        var addresses = new[]
        {
            $"/api/organizations/{organization}/projects/{foreign.ProjectId}/workitems/assignee-options",
            $"/api/organizations/{organization}/projects/{sleeping.Project.Id}/workitems/assignee-options",
            $"/api/organizations/{organization}/projects/{Guid.NewGuid()}/workitems/assignee-options",
            $"/api/organizations/{organization}/projects/not-a-guid/workitems/assignee-options",
        };

        foreach (var address in addresses)
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, address, token: OrganizationsTokens.SignedIn());

            Assert.Equal(HttpStatusCode.NotFound, answer.Status);
            Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        }
    }

    // ---- The filter --------------------------------------------------------------------------------------------------------------

    // US-2 "A former member's items can still be found": Ivan's BUG-2, and not his deleted US-5; the cell carries the marks (OQ-2 A).
    [Fact]
    public async Task Assignee_AFormerMember_IsFoundByHisLiveItems_AndTheRowCarriesTheMarks()
    {
        var data = await CreateAsync(factory);

        var answer = await ListAsync(factory, data.World, Query(assignees: [data.People.Ivan]));

        Assert.Equal(Of(data, "BUG2"), Identifiers(answer));
        Assert.Equal(1, Count(answer));
        var assignee = answer.Body.GetProperty("data")[0].GetProperty("assignee");
        Assert.Equal(
            ["displayName", "email", "id", "isActive", "isMember"],
            assignee.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("Иван Котов", true, false), (assignee.GetProperty("displayName").GetString(), assignee.GetProperty("isActive").GetBoolean(), assignee.GetProperty("isMember").GetBoolean()));
    }

    // US-2 "An inactive member's items can still be found".
    [Fact]
    public async Task Assignee_ADisabledAccount_IsFound_AndTheRowSaysNotActive()
    {
        var data = await CreateAsync(factory);

        var answer = await ListAsync(factory, data.World, Query(assignees: [data.People.Petr]));

        Assert.Equal(Of(data, "TASK4"), Identifiers(answer));
        var assignee = answer.Body.GetProperty("data")[0].GetProperty("assignee");
        Assert.Equal(("Пётр Ли", false, true), (assignee.GetProperty("displayName").GetString(), assignee.GetProperty("isActive").GetBoolean(), assignee.GetProperty("isMember").GetBoolean()));
    }

    // OQ-2 A / BR-WS-7 · a known active member's row: active and a member; an unknown user's: no name, not active.
    [Fact]
    public async Task Assignee_TheRowsFlags_AreTheDirectorysAndTheMemberships()
    {
        var data = await CreateAsync(factory);
        await WorkItemsTestData.ExecuteAsync(factory, $"DELETE FROM directory_users WHERE id = {data.People.Petr}");

        var answer = await ListAsync(factory, data.World, Query());
        var rows = answer.Body.GetProperty("data").EnumerateArray().ToDictionary(row => row.GetProperty("identifier").GetString()!);

        var mia = rows[data.Id("BUG42")].GetProperty("assignee");
        Assert.Equal(("Мия Петрова", true, true), (mia.GetProperty("displayName").GetString(), mia.GetProperty("isActive").GetBoolean(), mia.GetProperty("isMember").GetBoolean()));
        var unknown = rows[data.Id("TASK4")].GetProperty("assignee");
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("displayName").ValueKind);
        Assert.Equal(JsonValueKind.Null, unknown.GetProperty("email").ValueKind);
        Assert.False(unknown.GetProperty("isActive").GetBoolean());
        Assert.True(unknown.GetProperty("isMember").GetBoolean());
        Assert.Equal(JsonValueKind.Null, rows[data.Id("US3")].GetProperty("assignee").ValueKind);
    }

    // The card keeps its reading until feat-017: its assignee has no `isActive`.
    [Fact]
    public async Task Assignee_TheCardsAssignee_KeepsItsShape()
    {
        var data = await CreateAsync(factory);

        var card = await WorkItemsTestData.ReadAsync(factory, data.World, data.Id("BUG2"));

        Assert.Equal(
            ["displayName", "email", "id", "isMember"],
            card.GetProperty("assignee").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    // US-2 "«Не назначен» finds the unassigned items".
    [Fact]
    public async Task Assignee_None_FindsTheUnassignedItems()
    {
        var data = await CreateAsync(factory);

        Assert.Equal(Of(data, "US3"), await RowsAsync(data, assignees: ["none"]));
        Assert.Equal(Of(data, "US3"), await RowsAsync(data, states: ["Разрешена"], assignees: ["none"]));

        // Both deleted items have an assignee, so «Не назначен» under «Удалена» finds none of them.
        Assert.Empty(await RowsAsync(data, states: ["Удалена"], assignees: ["none"]));
        Assert.Equal(Of(data, "US3"), await RowsAsync(data, states: ["Удалена", "Разрешена"], assignees: ["none"]));
    }

    // US-2 "Any chosen value matches": Mia and «Не назначен», in the unfiltered list's order.
    [Fact]
    public async Task Assignee_SeveralValues_MatchAny_InTheOrderOfTheUnfilteredList()
    {
        var data = await CreateAsync(factory);

        Assert.Equal(Of(data, "BUG42", "US3", "US1"), await RowsAsync(data, assignees: [data.People.Mia, "none"]));
        Assert.Equal(Of(data, "BUG42", "US3", "TASK4", "US1"), await RowsAsync(data, assignees: ["none", data.People.Petr, data.People.Mia]));
    }

    // US-2 "With «Удалена» chosen, the people of deleted items are offered too": Ivan under «Удалена» is US-5 alone, and with «Активна»
    // added still US-5 alone, since BUG-2 is «Разрешена».
    [Fact]
    public async Task Assignee_WithUdalena_FindsTheDeletedItemsOfThePerson()
    {
        var data = await CreateAsync(factory);

        Assert.Equal(Of(data, "US5"), await RowsAsync(data, states: ["Удалена"], assignees: [data.People.Ivan]));
        Assert.Equal(Of(data, "US5"), await RowsAsync(data, states: ["Удалена", "Активна"], assignees: [data.People.Ivan]));
        Assert.Equal(Of(data, "US5", "BUG2"), await RowsAsync(data, states: ["Удалена", "Разрешена"], assignees: [data.People.Ivan]));
        Assert.Equal(Of(data, "FEAT6"), await RowsAsync(data, states: ["Удалена"], assignees: [data.People.Zoya]));
    }

    // US-2 "A person offered only under «Удалена» names nobody once «Удалена» is cleared": Zoya's FEAT-6 is deleted.
    [Fact]
    public async Task Assignee_APersonOfDeletedItemsOnly_SelectsNothing_WhileUdalenaIsNotChosen()
    {
        var data = await CreateAsync(factory);

        var answer = await ListAsync(factory, data.World, Query(assignees: [data.People.Zoya]));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Identifiers(answer));
        Assert.Equal(0, Count(answer));
        Assert.Empty(await RowsAsync(data, states: ["Новая"], assignees: [data.People.Zoya]));
    }

    // US-2 "The assignee filter combines with the state and team filters".
    [Fact]
    public async Task Assignee_CombinesWithTheStateAndTeamFilters_EachClearWidens()
    {
        var data = await CreateAsync(factory);
        var venus = new[] { data.Teams.Venus.ToString() };
        var mia = new[] { data.People.Mia };

        Assert.Equal(Of(data, "BUG42"), await RowsAsync(data, ["Новая"], mia, venus));
        Assert.Equal(Of(data, "BUG42"), await RowsAsync(data, null, mia, venus));
        Assert.Equal(Of(data, "BUG42"), await RowsAsync(data, ["Новая"], mia, null));
        Assert.Equal(Of(data, "BUG42", "US1"), await RowsAsync(data, null, mia, null));
    }

    // US-2 outline "A value that names nobody assigned in the project selects nothing, alike": a malformed id, one the directory does not
    // know, another organization's user, a member assigned nothing — the same status, count and body, and beside Mia's id only Mia's items.
    [Fact]
    public async Task Assignee_AValueThatNamesNobodyAssigned_SelectsNothing_AlikeForEveryKind()
    {
        var data = await CreateAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory, "OTH", "Чужой");
        var oleg = await WorkItemsTestData.AddMemberAsync(factory, other, "Олег", "Сидоров");
        var foreign = await WorkItemsTestData.CreateItemAsync(factory, other, "TASK", "Задача Олега");
        await WorkItemsTestData.SaveAsync(factory, other, foreign, new { assignee = new { userId = oleg } });

        var kinds = new[]
        {
            "not-a-guid", Guid.NewGuid().ToString(), oleg, data.People.Ann, data.People.Rita, "NONE", "None", data.People.Mia.ToUpperInvariant(), "%", "'; DROP TABLE work_items; --",
        };
        var bodies = new List<string>();

        foreach (var kind in kinds)
        {
            var answer = await ListAsync(factory, data.World, Query(assignees: [kind]));

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal(0, Count(answer));
            bodies.Add(answer.Body.GetRawText());

            var beside = await RowsAsync(data, assignees: [kind, data.People.Mia]);
            Assert.Equal(Of(data, "BUG42", "US1"), beside);
        }

        Assert.Single(bodies.Distinct());
    }

    // BR-WS-11 · a blank id is left out; BR-WS-10 · a NUL names nothing and is no error.
    [Fact]
    public async Task Assignee_BlankValuesAreLeftOut_AndANulNamesNothing()
    {
        var data = await CreateAsync(factory);
        var path = WorkItemsTestData.List(data.World);

        var blank = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path + "?assigneeId=&assigneeId=%20", token: OrganizationsTokens.SignedIn());
        var nul = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path + "?assigneeId=a%00b", token: OrganizationsTokens.SignedIn());
        var nulBeside = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, path + "?assigneeId=%00&assigneeId=" + data.People.Mia, token: OrganizationsTokens.SignedIn());
        var tooLong = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, path + "?assigneeId=" + new string('u', 5_000), token: OrganizationsTokens.SignedIn());

        Assert.Equal(5, Count(blank));
        Assert.Equal(HttpStatusCode.OK, nul.Status);
        Assert.Equal(0, Count(nul));
        Assert.Equal(Of(data, "BUG42", "US1"), Identifiers(nulBeside));
        Assert.Equal(HttpStatusCode.OK, tooLong.Status);
        Assert.Equal(0, Count(tooLong));
    }

    // US-2 "A reader filters by assignee".
    [Fact]
    public async Task Assignee_AReader_FiltersTheSameWay()
    {
        var data = await CreateAsync(factory);

        var answer = await ListAsync(factory, data.World, Query(assignees: [data.People.Mia]), OrganizationsTokens.SignedIn(data.People.Rita));

        Assert.Equal(Of(data, "BUG42", "US1"), Identifiers(answer));
    }

    // US-2 "The filter lives in the address, and a person the address names may have no item": Petr's only item is reassigned.
    [Fact]
    public async Task Assignee_APersonWhoseOnlyItemWasReassigned_IsNoLongerAnOption_AndSelectsNothing()
    {
        var data = await CreateAsync(factory);
        Assert.Equal(Of(data, "TASK4"), await RowsAsync(data, assignees: [data.People.Petr]));

        await WorkItemsTestData.SaveAsync(factory, data.World, data.Id("TASK4"), new { assignee = new { userId = data.People.Mia } });

        Assert.Empty(await RowsAsync(data, assignees: [data.People.Petr]));
        Assert.DoesNotContain(await OptionsOfAsync(data), option => option.Id == data.People.Petr);
    }

    // BR-WS-15 · another organization's person is no option and no match of this project, whatever ids the address names.
    [Fact]
    public async Task Assignee_NeverFindsAnotherOrganizationsItems()
    {
        var data = await CreateAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory, "OTH", "Чужой");
        var oleg = await WorkItemsTestData.AddMemberAsync(factory, other, "Олег", "Сидоров");
        var foreign = await WorkItemsTestData.CreateItemAsync(factory, other, "TASK", "Задача Олега");
        await WorkItemsTestData.SaveAsync(factory, other, foreign, new { assignee = new { userId = oleg } });

        Assert.Empty(await RowsAsync(data, assignees: [oleg]));
        Assert.Equal([foreign], Identifiers(await ListAsync(factory, other, Query(assignees: [oleg]))));
        Assert.DoesNotContain(await OptionsOfAsync(data), option => option.Id == oleg);
    }
}
