using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// One team order across projects (feat-040 api-contract § 1, § 3.2, § 8.1; US-3; BR-TS-6, BR-TS-7): the placed items first by
/// position, every other item and every placed item of an equal position by creation time, then project key by character code,
/// then number as a number; a reorder writes a dense place for every item of every active project and moves no project's own order;
/// an item that is not on the backlog is stale, alike; a deactivated project's places are kept; a reorder against an unlink, a team
/// deletion, a team change or a deactivation ends in one order with no server error.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamOrderAcrossProjectsEndpointTests(MustaskApiFactory factory)
{
    private const string Stale = "boards.backlog.reorder_stale";

    private static readonly DateTimeOffset Ten = new(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);

    private static string[] Ids(params Item[] items) => items.Select(item => item.Identifier).ToArray();

    private static string[] Sorted(params string[] values) => values.Order(StringComparer.Ordinal).ToArray();

    /// <summary>The team places of the three projects' items as <c>identifier:position</c>, ordered by identifier.</summary>
    private async Task<string[]> PlacesAsync(Cast cast, params Item[] items)
    {
        var places = (await Task.WhenAll(new[] { cast.Alpha, cast.Beta, cast.Gamma }.Select(view => ProjectBoardsTestData.TeamPlacesAsync(factory, view))))
            .SelectMany(rows => rows)
            .ToDictionary(place => place.ItemId, place => place.Position);

        return items.Where(item => places.ContainsKey(item.Id)).Select(item => $"{item.Identifier}:{places[item.Id]}").Order(StringComparer.Ordinal).ToArray();
    }

    // US-3 · "Unplaced items sort by creation time, then project key, then number" (BR-TS-6): BET-US-2 at 10:00, ALP-US-1 at 10:05,
    // ALP-US-3 and BET-US-3 both at 10:10, ALP-US-10 and ALP-US-9 both at 10:15 — the keys decide the 10:10 pair (ALP before BET)
    // and the number as a number the 10:15 pair (9 before 10). Twice, and on the board.
    [Fact]
    public async Task Get_Unplaced_StandByCreationTime_ThenProjectKey_ThenNumberAsANumber()
    {
        var cast = await CreateAsync(factory);
        var alpha = new List<Item>();

        for (var number = 1; number <= 10; number++)
        {
            alpha.Add(await StoryAsync(factory, cast.Alpha, $"A{number}", number is 1 or 3 or 9 or 10 ? cast.Mars : null));
        }

        var bet1 = await StoryAsync(factory, cast.Beta, "B1", null);
        var bet2 = await StoryAsync(factory, cast.Beta, "B2", cast.Mars);
        var bet3 = await StoryAsync(factory, cast.Beta, "B3", cast.Mars);
        Assert.Equal(["ALP-US-1", "ALP-US-3", "ALP-US-9", "ALP-US-10"], new[] { alpha[0], alpha[2], alpha[8], alpha[9] }.Select(item => item.Identifier));
        Assert.Equal(["BET-US-2", "BET-US-3"], Ids(bet2, bet3));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, bet2, Ten);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, alpha[0], Ten.AddMinutes(5));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, alpha[2], Ten.AddMinutes(10));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, bet3, Ten.AddMinutes(10));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, alpha[9], Ten.AddMinutes(15));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, alpha[8], Ten.AddMinutes(15));
        _ = bet1;

        string[] expected = ["BET-US-2", "ALP-US-1", "ALP-US-3", "BET-US-3", "ALP-US-9", "ALP-US-10"];

        Assert.Equal(expected, await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(expected, await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(expected, await CardsAsync(factory, cast, cast.Mars));
    }

    // US-3 · the placed items lead whatever their creation time; the unplaced follow by the tie-break.
    [Fact]
    public async Task Get_PlacedItemsLead_ThenTheUnplacedOnesByTheTieBreak()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, b1, g1, a1);
        var late = await StoryAsync(factory, cast.Alpha, "Поздняя", cast.Mars);
        var early = await StoryAsync(factory, cast.Gamma, "Ранняя, но не размещена", cast.Mars);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, g1, Ten.AddDays(3));
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, early, Ten.AddDays(-3));

        // A reorder places every item on the backlog; the items created after it are the unplaced ones, and follow the placed ones
        // — the one created earliest first — whatever the placed ones' creation time.
        Assert.Equal(Ids(b1, g1, a1, early, late), await RowsAsync(factory, cast, cast.Mars));
    }

    // US-3 · "Equal positions left by the per-project reorders sort the same way" (BR-TS-6, -14; feat-028 OQ-3 A): ALP-US-4 and
    // BET-US-7 hold the same position 1 and BET-US-7 was created first — it stands directly before ALP-US-4, and every read gives
    // the same order; the first reorder then gives every item a position of its own and keeps the order but for the moved item.
    [Fact]
    public async Task Get_EqualPositions_SortByTheTieBreak_AndTheFirstReorderMakesThemDense()
    {
        var cast = await CreateAsync(factory);
        var levelId = cast.World.Items.Types["US"].BacklogLevelId;
        var alp = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var bet = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var other = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, bet, Ten);
        await ProjectBoardsTestData.SetCreatedAtAsync(factory, alp, Ten.AddMinutes(1));
        await PlaceAsync(factory, alp.Id, cast.Mars, levelId, 1);
        await PlaceAsync(factory, bet.Id, cast.Mars, levelId, 1);

        var first = await RowsAsync(factory, cast, cast.Mars);
        var second = await RowsAsync(factory, cast, cast.Mars);
        var board = await CardsAsync(factory, cast, cast.Mars);

        Assert.Equal(Ids(bet, alp, other), first);
        Assert.Equal(first, second);
        Assert.Equal(first, board);

        var moved = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(other));

        Assert.Equal(HttpStatusCode.NoContent, moved.Status);
        Assert.Equal(Ids(other, bet, alp), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(
            Sorted($"{alp.Identifier}:3", $"{bet.Identifier}:2", $"{other.Identifier}:1"),
            await PlacesAsync(cast, alp, bet, other));
    }

    // US-3 · "A reorder ranks items of several projects and moves no project's order" (BR-TS-7; BR-MX-18): ALP-US-1, ALP-US-2,
    // BET-US-1, GAM-US-1; GAM-US-1 moved directly after ALP-US-1 — MARS's backlog and board list ALP-US-1, GAM-US-1, ALP-US-2,
    // BET-US-1, and the three projects' own backlogs keep their orders and place nothing.
    [Fact]
    public async Task Reorder_AcrossProjects_RanksTheirItemsInOneOrder_LeavesEveryProjectOrderAlone()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var a2 = await StoryAsync(factory, cast.Alpha, "A2", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, a2, b1, g1);
        var projectOrders = new[]
        {
            await ProjectBoardsTestData.RowsAsync(factory, cast.Alpha),
            await ProjectBoardsTestData.RowsAsync(factory, cast.Beta),
            await ProjectBoardsTestData.RowsAsync(factory, cast.Gamma),
        };

        var answer = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.After(g1, a1));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        string[] expected = Ids(a1, g1, a2, b1);
        Assert.Equal(expected, await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(expected, await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(expected, await CardsAsync(factory, cast, cast.Mars));
        Assert.Equal(projectOrders[0], await ProjectBoardsTestData.RowsAsync(factory, cast.Alpha));
        Assert.Equal(projectOrders[1], await ProjectBoardsTestData.RowsAsync(factory, cast.Beta));
        Assert.Equal(projectOrders[2], await ProjectBoardsTestData.RowsAsync(factory, cast.Gamma));
        foreach (var view in new[] { cast.Alpha, cast.Beta, cast.Gamma })
        {
            Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, view));
        }

        Assert.Equal(Sorted($"{a1.Identifier}:1", $"{a2.Identifier}:3", $"{b1.Identifier}:4", $"{g1.Identifier}:2"), await PlacesAsync(cast, a1, a2, b1, g1));
    }

    // US-3 · "A reorder naming an item that is not on the backlog is stale and discloses nothing" (BR-TS-7): an item of VENUS, an item
    // of Гамма while Гамма is deactivated, an item of MARS in «Удалена», an item of another organization and an unknown id — as the item
    // moved and as the one it is placed after: every one 409 with the same code and text, and nothing changes.
    [Fact]
    public async Task Reorder_NamingAnItemNotOnTheBacklog_IsStale_AlikeAndDisclosesNothing()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var venus = await StoryAsync(factory, cast.Alpha, "Венерина", cast.Venus);
        var gamma = await StoryAsync(factory, cast.Gamma, "Гаммы", cast.Mars);
        var removed = await StoryAsync(factory, cast.Beta, "Удалённая", cast.Mars);
        await LinksTestData.DeleteAsync(factory, cast.Beta.Items, removed);
        var foreign = await BoardsTestData.CreateWorldAsync(factory);
        var zeta = await BoardsTestData.CreateAsync(factory, foreign, "US", "Зета", foreign.PlatformAreaId);
        await OrderAsync(factory, cast, cast.Mars, a1, b1, gamma);
        await DeactivateAsync(factory, cast, cast.Gamma);
        var placesBefore = await PlacesAsync(cast, a1, b1, gamma);
        var named = new Item[] { venus, gamma, removed, zeta, new(Guid.CreateVersion7(), "ALP-US-999") };
        var answers = new List<Answer>();

        foreach (var item in named)
        {
            answers.Add(await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(item)));
            answers.Add(await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.After(a1, item)));
        }

        Assert.All(answers, answer => Assert.Equal((HttpStatusCode.Conflict, Stale), (answer.Status, answer.ErrorCode)));
        Assert.Single(answers.Select(answer => answer.Detail).Distinct());
        Assert.Equal(Ids(a1, b1), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(placesBefore, await PlacesAsync(cast, a1, b1, gamma));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, venus.Id));
    }

    // The five stale cases also for a body that is malformed — an id that is no id names nothing on the backlog.
    [Fact]
    public async Task Reorder_AMalformedItemId_IsStale()
    {
        var cast = await CreateAsync(factory);
        await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);

        var moved = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{Team(cast, cast.Mars)}/backlogs/stories/reorder", new { itemId = "not-an-id", position = "top" }, OrganizationsTokens.SignedIn());
        var anchored = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{Team(cast, cast.Mars)}/backlogs/stories/reorder", new { itemId = Guid.CreateVersion7().ToString(), position = "after", afterItemId = "junk" }, OrganizationsTokens.SignedIn());

        Assert.Equal((HttpStatusCode.Conflict, Stale), (moved.Status, moved.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, Stale), (anchored.Status, anchored.ErrorCode));
    }

    // US-3 · "A deactivated project's places are kept by a reorder" (BR-TS-7; ADR-0028 rule 9): ALP-US-1, GAM-US-1, BET-US-1; Гамма
    // deactivated; BET-US-1 to the top — MARS lists BET-US-1, ALP-US-1; GAM-US-1 keeps the position it had, the reorder wrote none for
    // it; restored, it stands by that position among the positions the move wrote, a tie sorting by BR-TS-6, and Гамма's own backlog
    // has its former order.
    [Fact]
    public async Task Reorder_KeepsADeactivatedProjectsPlaces_AndTheRestoreBringsThemBack()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, g1, b1);
        var gammaOrder = await ProjectBoardsTestData.RowsAsync(factory, cast.Gamma);
        Assert.Equal(Sorted($"{a1.Identifier}:1", $"{b1.Identifier}:3", $"{g1.Identifier}:2"), await PlacesAsync(cast, a1, g1, b1));
        await DeactivateAsync(factory, cast, cast.Gamma);

        var answer = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(Ids(b1, a1), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(Sorted($"{a1.Identifier}:2", $"{b1.Identifier}:1", $"{g1.Identifier}:2"), await PlacesAsync(cast, a1, g1, b1));

        await RestoreAsync(factory, cast, cast.Gamma);

        var restored = await RowsAsync(factory, cast, cast.Mars);
        Assert.Equal(Ids(b1, a1, g1), restored);
        Assert.Equal(restored, await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(gammaOrder, await ProjectBoardsTestData.RowsAsync(factory, cast.Gamma));
    }

    // US-3 / BR-TS-7 · "the places a reorder drops are those of items that left, in the organization's active projects": an item that
    // was deleted loses its place; a deactivated project's deleted item keeps its own (no reorder reaches it); the places of items still
    // on the backlog are rewritten, none dropped.
    [Fact]
    public async Task Reorder_DropsTheDepartedPlacesOfTheActiveProjects_KeepsADeactivatedProjectsPlaces()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var gone = await StoryAsync(factory, cast.Beta, "Уйдёт", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        var g2 = await StoryAsync(factory, cast.Gamma, "G2", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, gone, b1, g1, g2);
        await LinksTestData.DeleteAsync(factory, cast.Beta.Items, gone);
        await LinksTestData.DeleteAsync(factory, cast.Gamma.Items, g2);
        Assert.Equal(1, await TeamPlacesOfAsync(factory, gone.Id));
        await DeactivateAsync(factory, cast, cast.Gamma);

        var answer = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(0, await TeamPlacesOfAsync(factory, gone.Id));
        Assert.Equal(1, await TeamPlacesOfAsync(factory, g2.Id));
        Assert.Equal(1, await TeamPlacesOfAsync(factory, g1.Id));
        Assert.Equal(Ids(b1, a1), await RowsAsync(factory, cast, cast.Mars));
    }

    // US-3 · "Who may reorder a team's backlog" (BR-TS-13): Ann («Администратор») and Max («Участник») move an item, Vera («Читатель»)
    // is refused 403 and nothing changes — also naming an unknown team, where the 403 comes first.
    [Fact]
    public async Task Reorder_Roles_AdministratorAndMemberMove_ViewerIs403BeforeAnyNotFound()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var ann = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Admin);
        var max = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Member);
        var vera = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Viewer);
        await OrderAsync(factory, cast, cast.Mars, a1, b1);

        var viewer = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1), token: vera.Token);
        var viewerUnknownTeam = await ReorderAsync(factory, cast, Guid.CreateVersion7(), BoardsTestData.Top(b1), token: vera.Token);
        Assert.Equal(Ids(a1, b1), await RowsAsync(factory, cast, cast.Mars));
        var member = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1), token: max.Token);
        var administrator = await ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(a1), token: ann.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (viewer.Status, viewer.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (viewerUnknownTeam.Status, viewerUnknownTeam.ErrorCode));
        Assert.Equal(HttpStatusCode.NoContent, member.Status);
        Assert.Equal(HttpStatusCode.NoContent, administrator.Status);
        Assert.Equal(Ids(a1, b1), await RowsAsync(factory, cast, cast.Mars, token: vera.Token));
    }

    // A team linked to no active project has no member: the reorder ends 409 (nothing to move), not 404 (api-contract § 3.2).
    [Fact]
    public async Task Reorder_ATeamWithNoActiveProject_IsStale_NotNotFound()
    {
        var cast = await CreateAsync(factory);

        var answer = await ReorderAsync(factory, cast, cast.Lonely, BoardsTestData.Top(new Item(Guid.CreateVersion7(), "X-US-1")));

        Assert.Equal((HttpStatusCode.Conflict, Stale), (answer.Status, answer.ErrorCode));
    }

    // --- The races (api-contract § 8.1, each ends in one order and no server error) --------------------------------------------

    // The reorder first, held at its commit with its member locks: the unlink of Бета from MARS, which reassigns Бета's items under
    // FOR UPDATE, waits for it; the reorder commits, the unlink then drops the places of Бета's items and hands them to nobody.
    [Fact]
    public async Task Race_ReorderFirst_ThenTheUnlinkOfAProject_DropsThatProjectsPlaces()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, b1, g1);

        var (reorder, unlink) = await TeamTestData.RaceAsync(
            factory,
            gate => ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1), gate: gate),
            _ => TeamTestData.UnlinkAsync(factory, cast.OrganizationId, cast.Beta.ProjectId, cast.Mars, TeamTestData.To(null)));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (reorder.Status, unlink.Status));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, b1.Id));
        Assert.Equal(Ids(a1, g1), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(0, await PlacesNamingAnotherTeamAsync(factory, cast.Beta.ProjectId));
    }

    // The unlink first, held at its commit with the owned items locked: the reorder waits for them, then finds Бета's item no longer
    // MARS's — moving it is stale; moving another item completes on the remaining members.
    [Fact]
    public async Task Race_UnlinkFirst_ThenAReorderMovingTheUnlinkedItem_IsStale()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, b1, g1);

        var (unlink, reorder) = await TeamTestData.RaceAsync(
            factory,
            gate => TeamTestData.UnlinkAsync(factory, cast.OrganizationId, cast.Beta.ProjectId, cast.Mars, TeamTestData.To(null), gate: gate),
            _ => ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1)));

        Assert.Equal(HttpStatusCode.NoContent, unlink.Status);
        Assert.Equal((HttpStatusCode.Conflict, Stale), (reorder.Status, reorder.ErrorCode));
        Assert.Equal(Ids(a1, g1), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(0, await PlacesNamingAnotherTeamAsync(factory, cast.Beta.ProjectId));
    }

    [Fact]
    public async Task Race_UnlinkFirst_ThenAReorderMovingAnotherItem_CompletesOnTheRemainingMembers()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, b1, g1);

        var (unlink, reorder) = await TeamTestData.RaceAsync(
            factory,
            gate => TeamTestData.UnlinkAsync(factory, cast.OrganizationId, cast.Beta.ProjectId, cast.Mars, TeamTestData.To(null), gate: gate),
            _ => ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(g1)));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (unlink.Status, reorder.Status));
        Assert.Equal(Ids(g1, a1), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, b1.Id));
    }

    // A deletion of the team waits for the reorder that holds the team row FOR SHARE: the reorder commits first and the deletion then
    // removes every place, so nothing is left for a team that is gone.
    [Fact]
    public async Task Race_ReorderFirst_ThenTheTeamsDeletion_RemovesEveryPlace()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, b1);

        var (reorder, deletion) = await TeamTestData.RaceAsync(
            factory,
            gate => ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1), gate: gate),
            _ => TeamTestData.DeleteTeamAsync(factory, cast.OrganizationId, cast.Mars));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (reorder.Status, deletion.Status));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, a1.Id) + await TeamPlacesOfAsync(factory, b1.Id));
        BoardsTestData.AssertRefused(await GetBacklogAsync(factory, cast, cast.Mars), HttpStatusCode.NotFound, "organizations.team.not_found");
    }

    // The deletion first, held at its commit: the reorder waits on the team, then answers «Команда не найдена.» and places nothing.
    [Fact]
    public async Task Race_TheTeamsDeletionFirst_ThenTheReorder_IsTeamNotFound()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);

        var (deletion, reorder) = await TeamTestData.RaceAsync(
            factory,
            gate => TeamTestData.DeleteTeamAsync(factory, cast.OrganizationId, cast.Mars, gate: gate),
            _ => ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(b1)));

        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);
        BoardsTestData.AssertRefused(reorder, HttpStatusCode.NotFound, "organizations.team.not_found");
        Assert.Equal(0, await TeamPlacesOfAsync(factory, a1.Id) + await TeamPlacesOfAsync(factory, b1.Id));
    }

    // A deactivation of one of MARS's projects takes no lock the reorder holds and the reorder takes none of the project's: the
    // deactivation commits while the reorder is held at its commit; the reorder then completes on the members it read — Гамма's
    // items included — and Гамма's place stays (written by it), never dropped.
    [Fact]
    public async Task Race_ADeactivationWhileTheReorderIsHeld_IsNotSerialized_TheProjectsPlaceIsKept()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, g1);
        using var gate = factory.CommitGates.Open();

        var reorder = ReorderAsync(factory, cast, cast.Mars, BoardsTestData.Top(g1), gate: gate);
        await Task.WhenAny(gate.Reached, reorder);
        Assert.False(reorder.IsCompleted, "The reorder finished before reaching its commit gate.");
        var deactivation = DeactivateAsync(factory, cast, cast.Gamma);
        await Task.WhenAny(deactivation, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.True(deactivation.IsCompletedSuccessfully, "The deactivation waited for the reorder: a project row was locked.");

        gate.Release();
        var answer = await reorder;

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(1, await TeamPlacesOfAsync(factory, g1.Id));
        Assert.Equal(Ids(a1), await RowsAsync(factory, cast, cast.Mars));
        await RestoreAsync(factory, cast, cast.Gamma);
        Assert.Equal(Ids(g1, a1), await RowsAsync(factory, cast, cast.Mars));
    }

    // US-3 / tech-011 m2 · a read that meets the team's deletion: before the committed deletion the view as it stood, after it
    // «Команда не найдена.» — the two plain orders; the interleavings (between the team check and the read, and after it) are
    // TeamViewDeletionRaceTests', at the read model.
    [Fact]
    public async Task Get_BeforeAndAfterTheTeamsDeletion_IsTheViewThenTeamNotFound_NeverAServerError()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);

        var before = await GetBacklogAsync(factory, cast, cast.Mars);
        var boardBefore = await GetBoardAsync(factory, cast, cast.Mars);
        var deletion = await TeamTestData.DeleteTeamAsync(factory, cast.OrganizationId, cast.Mars);
        var after = await GetBacklogAsync(factory, cast, cast.Mars);
        var boardAfter = await GetBoardAsync(factory, cast, cast.Mars);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal(HttpStatusCode.OK, boardBefore.Status);
        Assert.Equal([a1.Identifier], before.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()));
        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);
        BoardsTestData.AssertRefused(after, HttpStatusCode.NotFound, "organizations.team.not_found");
        BoardsTestData.AssertRefused(boardAfter, HttpStatusCode.NotFound, "organizations.team.not_found");
    }
}
