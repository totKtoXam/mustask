using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.WorkItems.SearchTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// The «Статус» filter and the way into the recycle bin (feat-003 US-1, BR-WS-3, -4, -5, -10, -12; api-contract § 2.1, § 3.1),
/// through the endpoint and a real PostgreSQL, over the spec's world (<see cref="SearchTestData"/>): the live items MSK-BUG-42,
/// US-3, TASK-4, BUG-2, US-1 and the deleted FEAT-6 and US-5, which are listed only while «Удалена» is among the chosen names.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemStateFilterEndpointTests(MustaskApiFactory factory)
{
    private async Task<(string[] Rows, int Count)> ReadAsync(SearchWorld data, string query)
    {
        var answer = await ListAsync(factory, data.World, query);

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return (Identifiers(answer), Count(answer));
    }

    // US-1 "No filter": a deleted item is in no row and no count, as mod-005 BR-DR-6 has it.
    [Fact]
    public async Task List_NoFilter_HoldsTheFiveLiveItems_AndNeverTheDeletedOnes()
    {
        var data = await CreateAsync(factory);

        var (rows, count) = await ReadAsync(data, string.Empty);

        Assert.Equal(Of(data, "BUG42", "US3", "TASK4", "BUG2", "US1"), rows);
        Assert.Equal(5, count);
    }

    // US-1 "One state narrows the list, and a deleted item's held state does not count": FEAT-6 held «Новая» when deleted.
    [Fact]
    public async Task State_One_NarrowsTheList_AndADeletedItemsHeldStateDoesNotCount()
    {
        var data = await CreateAsync(factory);

        var (rows, count) = await ReadAsync(data, Query(states: ["Новая"]));

        Assert.Equal(Of(data, "BUG42"), rows);
        Assert.Equal(1, count);
        Assert.Equal(Of(data, "US1"), (await ReadAsync(data, Query(states: ["Активна"]))).Rows);
        Assert.Equal(Of(data, "TASK4"), (await ReadAsync(data, Query(states: ["Закрыта"]))).Rows);
    }

    // US-1 "A state name spans the types that have it": «Разрешена» finds a story and a bug, and no task (its type has none).
    [Fact]
    public async Task State_AName_SpansTheTypesThatHaveIt()
    {
        var data = await CreateAsync(factory);

        var (rows, count) = await ReadAsync(data, Query(states: ["Разрешена"]));

        Assert.Equal(Of(data, "US3", "BUG2"), rows);
        Assert.Equal(2, count);
    }

    // US-1 "Any chosen state matches, and the rows keep their order": the unfiltered list's order.
    [Fact]
    public async Task State_SeveralNames_MatchAny_InTheOrderOfTheUnfilteredList()
    {
        var data = await CreateAsync(factory);

        var (rows, count) = await ReadAsync(data, Query(states: ["Новая", "Разрешена"]));
        var (reversed, _) = await ReadAsync(data, Query(states: ["Разрешена", "Новая"]));

        Assert.Equal(Of(data, "BUG42", "US3", "BUG2"), rows);
        Assert.Equal(3, count);
        Assert.Equal(rows, reversed);
    }

    // US-1 "«Удалена» lists the recycle bin": FEAT-6 then US-5, latest change first; each row reads «Удалена» in the Removed category,
    // its «Изменён» is its last change, and the row has no column of its own (OQ-3 A).
    [Fact]
    public async Task State_Udalena_ListsTheDeletedItems_LatestChangeFirst()
    {
        var data = await CreateAsync(factory);

        var answer = await ListAsync(factory, data.World, Query(states: ["Удалена"]));

        Assert.Equal(Of(data, "FEAT6", "US5"), Identifiers(answer));
        Assert.Equal(2, Count(answer));

        foreach (var row in answer.Body.GetProperty("data").EnumerateArray())
        {
            var state = row.GetProperty("state");

            Assert.Equal("Удалена", state.GetProperty("name").GetString());
            Assert.Equal("Removed", state.GetProperty("category").GetProperty("code").GetString());
            Assert.False(row.TryGetProperty("deletion", out _));
            Assert.Equal(
                ["assignee", "changedAt", "id", "identifier", "number", "state", "team", "title", "type"],
                row.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        }
    }

    // US-1 "«Удалена» beside another state lists both": the deleted rows are among the live ones, in the one order.
    [Fact]
    public async Task State_UdalenaBesideAnother_ListsBoth()
    {
        var data = await CreateAsync(factory);

        var (rows, count) = await ReadAsync(data, Query(states: ["Активна", "Удалена"]));

        Assert.Equal(Of(data, "FEAT6", "US5", "US1"), rows);
        Assert.Equal(3, count);
    }

    // US-1 "The state filter combines with the team filter".
    [Fact]
    public async Task State_CombinesWithTheTeamFilter()
    {
        var data = await CreateAsync(factory);
        var venus = data.Teams.Venus.ToString();

        Assert.Equal(Of(data, "BUG2"), (await ReadAsync(data, Query(states: ["Разрешена"], teams: [venus]))).Rows);
        Assert.Equal(Of(data, "FEAT6"), (await ReadAsync(data, Query(states: ["Удалена"], teams: [venus]))).Rows);
        Assert.Equal(Of(data, "US5"), (await ReadAsync(data, Query(states: ["Удалена"], teams: [data.Teams.Mars.ToString()]))).Rows);
        Assert.Empty((await ReadAsync(data, Query(states: ["Закрыта"], teams: [venus]))).Rows);
    }

    // BR-WS-12 · the count follows the filter and a page past the last answers no rows and the right count; every page holds an item
    // once, though the live and the deleted items are two entry points of one statement.
    [Fact]
    public async Task State_LiveAndDeleted_PageByFifty_EachItemOnce_WithTheRightCount()
    {
        var data = await CreateAsync(factory);
        var world = data.World;
        var types = world.Types;

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id, created_at, created_by, changed_at)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {types["TASK"].Id}, {types["TASK"].StateId("Новая")},
                   'Живая ' || n, '', gen_random_uuid(), now(), 'paging-test', now() + n * interval '1 second'
            FROM generate_series(100, 169) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id, created_at, created_by, changed_at,
                                    deleted_from_state_id, deleted_by, deleted_at)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {types["TASK"].Id}, {types["TASK"].StateId(Mustask.Domain.Process.StateCategory.Removed)},
                   'Удалённая ' || n, '', gen_random_uuid(), now(), 'paging-test', now() + n * interval '1 second',
                   {types["TASK"].StateId("Новая")}, 'paging-test', now()
            FROM generate_series(200, 239) AS n
            """);

        // 70 live «Новая» tasks beside the data's one, 40 deleted tasks beside its two.
        var query = Query(states: ["Новая", "Удалена"]);
        var first = await ListAsync(factory, world, query);
        var second = await ListAsync(factory, world, Query(states: ["Новая", "Удалена"], page: 2));
        var third = await ListAsync(factory, world, Query(states: ["Новая", "Удалена"], page: 3));
        var past = await ListAsync(factory, world, Query(states: ["Новая", "Удалена"], page: 4));

        Assert.Equal(113, Count(first));
        Assert.Equal([50, 50, 13, 0], new[] { Identifiers(first).Length, Identifiers(second).Length, Identifiers(third).Length, Identifiers(past).Length });
        Assert.All(new[] { second, third, past }, answer => Assert.Equal(113, Count(answer)));

        var seen = Identifiers(first).Concat(Identifiers(second)).Concat(Identifiers(third)).ToList();
        Assert.Equal(113, seen.Distinct().Count());

        // Only «Удалена»: the 40 + 2 deleted ones, and the live ones never.
        var bin = await ListAsync(factory, world, Query(states: ["Удалена"]));
        Assert.Equal(42, Count(bin));
        Assert.DoesNotContain(data.Id("BUG42"), Identifiers(bin));
    }

    // US-1 "An address value that names no state selects nothing", and BR-WS-3: a name is compared as stored, letter case included.
    [Theory]
    [InlineData("Готово")]
    [InlineData("новая")]
    [InlineData("НОВАЯ")]
    [InlineData("Новая ")]
    [InlineData(" Новая")]
    [InlineData("удалена")]
    [InlineData("Новая%")]
    [InlineData("Н_вая")]
    [InlineData("'; DROP TABLE work_items; --")]
    public async Task State_ANameNoStateBears_SelectsNothing_AndBesideAValidOneAddsNothing(string name)
    {
        var data = await CreateAsync(factory);

        var (rows, count) = await ReadAsync(data, Query(states: [name]));
        var (beside, besideCount) = await ReadAsync(data, Query(states: [name, "Новая"]));

        Assert.Empty(rows);
        Assert.Equal(0, count);
        Assert.Equal(Of(data, "BUG42"), beside);
        Assert.Equal(1, besideCount);
    }

    // BR-WS-11 · a blank value is left out: a parameter of blank values sets no filter; one beside a real value adds nothing.
    [Fact]
    public async Task State_BlankValues_AreLeftOut()
    {
        var data = await CreateAsync(factory);
        var path = WorkItemsTestData.List(data.World);

        var blank = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path + "?state=&state=%20", token: OrganizationsTokens.SignedIn());
        var beside = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path + "?state=&state=" + Uri.EscapeDataString("Новая"), token: OrganizationsTokens.SignedIn());

        Assert.Equal(Of(data, "BUG42", "US3", "TASK4", "BUG2", "US1"), Identifiers(blank));
        Assert.Equal(Of(data, "BUG42"), Identifiers(beside));
    }

    // R-6 · U+0000 cannot be carried by PostgreSQL text: the value names nothing and is never an error.
    [Fact]
    public async Task State_ANulInAName_NamesNothing_AndIsNoError()
    {
        var data = await CreateAsync(factory);
        var path = WorkItemsTestData.List(data.World);

        var alone = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path + "?state=%D0%9D%00", token: OrganizationsTokens.SignedIn());
        var beside = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, path + "?state=%00&state=" + Uri.EscapeDataString("Новая"), token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, alone.Status);
        Assert.Empty(Identifiers(alone));
        Assert.Equal(Of(data, "BUG42"), Identifiers(beside));
    }

    // BR-WS-10 · a name longer than any state's selects nothing and is no error.
    [Fact]
    public async Task State_ANameLongerThanAnyState_SelectsNothing_AndIsNoError()
    {
        var data = await CreateAsync(factory);

        var (rows, count) = await ReadAsync(data, Query(states: [new string('я', 5_000)]));

        Assert.Empty(rows);
        Assert.Equal(0, count);
    }

    // US-1 "A deleted row opens its read-only card, and a restore takes it out of the bin": restored into «Активна», US-5 leaves
    // «Удалена» and is listed under «Активна» again.
    [Fact]
    public async Task State_ARestore_TakesTheItemOutOfTheBin_IntoItsState()
    {
        var data = await CreateAsync(factory);
        var us5 = data.Items["US5"];

        var restored = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.Item(data.World, us5.Identifier) + "/restore",
            new { stateId = data.World.Types["US"].StateId("Активна").ToString() },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, restored.Status);

        Assert.Equal(Of(data, "FEAT6"), (await ReadAsync(data, Query(states: ["Удалена"]))).Rows);
        Assert.Contains(us5.Identifier, (await ReadAsync(data, Query(states: ["Активна"]))).Rows);
        Assert.Contains(us5.Identifier, (await ReadAsync(data, string.Empty)).Rows);
    }

    // BR-WS-5 · after a purge the item is in no list, whatever the filter.
    [Fact]
    public async Task State_APurge_RemovesTheItemFromTheBin()
    {
        var data = await CreateAsync(factory);
        var feat6 = data.Items["FEAT6"];

        var purged = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, WorkItemsTestData.Item(data.World, feat6.Identifier) + "/purge", token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);

        var (rows, count) = await ReadAsync(data, Query(states: ["Удалена"]));

        Assert.Equal(Of(data, "US5"), rows);
        Assert.Equal(1, count);
    }

    // US-1 "Each role reaches the bin": the read code is every role's (ADR-0029 § 7), so an administrator, a member and a reader all
    // list MSK-FEAT-6 and MSK-US-5 — and none of the actions is in the row.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task State_EveryRole_ReachesTheBin(OrganizationRole role)
    {
        var data = await CreateAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, data.World.OrganizationId, role);

        var answer = await ListAsync(factory, data.World, Query(states: ["Удалена"]), member.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(Of(data, "FEAT6", "US5"), Identifiers(answer));
    }

    // US-1 / spec § Roles · a non-member, an instance administrator included, gets the organization's 404 whatever the filter is.
    [Fact]
    public async Task State_ANonMember_GetsTheOrganizationsNotFound_FromTheBinToo()
    {
        var data = await CreateAsync(factory);

        var stranger = await ListAsync(factory, data.World, Query(states: ["Удалена"]), MembershipTestData.Outsider());
        var administrator = await ListAsync(factory, data.World, Query(states: ["Удалена"]), OrganizationsTokens.WithCreateRight());

        foreach (var answer in new[] { stranger, administrator })
        {
            Assert.Equal(HttpStatusCode.NotFound, answer.Status);
            Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        }
    }

    // BR-WS-15 · the bin of one project never lists another project's or organization's deleted items.
    [Fact]
    public async Task State_TheBin_ListsOnlyThisProjectsDeletedItems()
    {
        var data = await CreateAsync(factory);
        var other = data.World with { ProjectId = data.Teams.OtherProjectId, Key = "BET" };
        var foreign = await WorkItemsTestData.CreateItemAsync(factory, other, "US", "Чужая удалённая");
        var foreignItem = (await WorkItemsTestData.ReadAsync(factory, other, foreign)).GetProperty("id").GetGuid();
        await LinksTestData.DeleteAsync(factory, other, new Item(foreignItem, foreign));

        Assert.Equal(Of(data, "FEAT6", "US5"), (await ReadAsync(data, Query(states: ["Удалена"]))).Rows);
        Assert.Equal([foreign], Identifiers(await ListAsync(factory, other, Query(states: ["Удалена"]))));
    }

    // BR-WS-13 · a project whose items are all deleted keeps the way into the bin: no live item, and «Удалена» lists it.
    [Fact]
    public async Task State_AProjectOfDeletedItemsOnly_StillListsThemUnderUdalena()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory, "PST", "Пустой");
        var only = await WorkItemsTestData.CreateItemAsync(factory, world, "US", "Единственная");
        var id = (await WorkItemsTestData.ReadAsync(factory, world, only)).GetProperty("id").GetGuid();
        await LinksTestData.DeleteAsync(factory, world, new Item(id, only));

        var unfiltered = await ListAsync(factory, world, string.Empty);
        var bin = await ListAsync(factory, world, Query(states: ["Удалена"]));

        Assert.Equal(0, Count(unfiltered));
        Assert.Equal([only], Identifiers(bin));
    }

    // BR-WS-17 · "One read of a page shows an item at most once" — the same across a filter that reaches the bin.
    [Fact]
    public async Task State_EveryStateChosen_ListsEveryItemOnce()
    {
        var data = await CreateAsync(factory);

        var (rows, count) = await ReadAsync(data, Query(states: ["Новая", "Активна", "Разрешена", "Закрыта", "Удалена"]));

        Assert.Equal(7, count);
        Assert.Equal(Of(data, "FEAT6", "US5", "BUG42", "US3", "TASK4", "BUG2", "US1"), rows);
    }
}
