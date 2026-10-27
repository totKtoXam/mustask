using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The project filter of a team's backlog and board (feat-040 api-contract § 3.1, § 3.3; US-2; BR-TS-8): the repeated
/// <c>projectId</c> query value, the shared filter specification's second dimension. Any chosen project matches; a value that names
/// no linked active project of the team's items selects nothing, none is an error, and an invalid one beside a valid one adds
/// nothing; it narrows the view in the same statement and changes no item, team, place or order; the tree is built over the
/// matching items and a checklist is whole; a reorder ignores it. The options (<c>projects</c>) are never narrowed.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamProjectFilterEndpointTests(MustaskApiFactory factory)
{
    private sealed record Seven(Cast Cast, Item A1, Item A2, Item B1, Item B2, Item B3, Item G1, Item G2);

    /// <summary>MARS's seven stories — two in Альфа, three in Бета, two in Гамма — put in the order A1, B1, A2, B2, B3, G1, G2.</summary>
    private async Task<Seven> SevenAsync()
    {
        var cast = await CreateAsync(factory);
        var a1 = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars);
        var a2 = await StoryAsync(factory, cast.Alpha, "A2", cast.Mars);
        var b1 = await StoryAsync(factory, cast.Beta, "B1", cast.Mars);
        var b2 = await StoryAsync(factory, cast.Beta, "B2", cast.Mars);
        var b3 = await StoryAsync(factory, cast.Beta, "B3", cast.Mars);
        var g1 = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
        var g2 = await StoryAsync(factory, cast.Gamma, "G2", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, a1, b1, a2, b2, b3, g1, g2);

        return new Seven(cast, a1, a2, b1, b2, b3, g1, g2);
    }

    private static string[] Ids(params Item[] items) => items.Select(item => item.Identifier).ToArray();

    // US-2 · "The filter offers the team's projects and narrows the view": one project, two projects, none — each narrows the
    // backlog and the board to the matching items in MARS's one order; `projects` (the options) is the same three whatever is chosen.
    [Fact]
    public async Task Filter_OneTwoOrNoProjects_NarrowsTheBacklogAndTheBoard_InTheTeamsOrder_OptionsNeverNarrowed()
    {
        var s = await SevenAsync();
        var (cast, beta, alpha) = (s.Cast, Filter(s.Cast.Beta.ProjectId), Filter(s.Cast.Alpha.ProjectId, s.Cast.Beta.ProjectId));

        Assert.Equal(Ids(s.B1, s.B2, s.B3), await RowsAsync(factory, cast, cast.Mars, query: beta));
        Assert.Equal(Ids(s.B1, s.B2, s.B3), await CardsAsync(factory, cast, cast.Mars, query: beta));
        Assert.Equal(Ids(s.A1, s.B1, s.A2, s.B2, s.B3), await RowsAsync(factory, cast, cast.Mars, query: alpha));
        Assert.Equal(Ids(s.A1, s.B1, s.A2, s.B2, s.B3), await CardsAsync(factory, cast, cast.Mars, query: alpha));
        Assert.Equal(Ids(s.A1, s.B1, s.A2, s.B2, s.B3, s.G1, s.G2), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(Ids(s.A1, s.B1, s.A2, s.B2, s.B3, s.G1, s.G2), await RowsAsync(factory, cast, cast.Mars, query: string.Empty));

        string[] options = [Wire(cast.Alpha, "Альфа"), Wire(cast.Beta, "Бета"), Wire(cast.Gamma, "Гамма")];
        Assert.Equal(options, ProjectsOf(await GetBacklogAsync(factory, cast, cast.Mars, query: beta)));
        Assert.Equal(options, ProjectsOf(await GetBoardAsync(factory, cast, cast.Mars, query: beta)));
    }

    // The id is read in either letter case, as every id of the API (mod-006 § 0.2).
    [Fact]
    public async Task Filter_AProjectIdInCapitals_Matches()
    {
        var s = await SevenAsync();

        var rows = await RowsAsync(factory, s.Cast, s.Cast.Mars, query: Filter(s.Cast.Gamma.ProjectId.ToString().ToUpperInvariant()));

        Assert.Equal(Ids(s.G1, s.G2), rows);
    }

    // US-2 · "The filter narrows the view and changes nothing" (mod-017 BR-OT-8): after filtered reads the unfiltered view of anyone
    // holds all seven, and no item, owning team, team place or project order has changed.
    [Fact]
    public async Task Filter_ChangesNothing_NoItemTeamPlaceOrProjectOrder()
    {
        var s = await SevenAsync();
        var cast = s.Cast;
        var stateBefore = await StateAsync(cast);

        await RowsAsync(factory, cast, cast.Mars, query: Filter(cast.Beta.ProjectId));
        await CardsAsync(factory, cast, cast.Mars, query: Filter(cast.Beta.ProjectId, cast.Gamma.ProjectId));
        await RowsAsync(factory, cast, cast.Mars, query: Filter(Guid.CreateVersion7()));

        Assert.Equal(stateBefore, await StateAsync(cast));
        Assert.Equal(Ids(s.A1, s.B1, s.A2, s.B2, s.B3, s.G1, s.G2), await RowsAsync(factory, cast, cast.Mars));
    }

    /// <summary>The owners and the team places of the three projects, and their own orders, as stored.</summary>
    private async Task<string> StateAsync(Cast cast)
    {
        var projects = new[] { cast.Alpha, cast.Beta, cast.Gamma };
        var teams = string.Join("|", await Task.WhenAll(projects.Select(project => TeamStateAsync(factory, project.ProjectId))));
        var teamPlaces = string.Join(";", (await Task.WhenAll(projects.Select(project => ProjectBoardsTestData.TeamPlacesAsync(factory, project))))
            .SelectMany(places => places).OrderBy(place => place.ItemId).Select(place => $"{place.ItemId}:{place.Position}"));
        var projectPlaces = string.Join(";", (await Task.WhenAll(projects.Select(project => ProjectBoardsTestData.PlacesAsync(factory, project))))
            .SelectMany(places => places).OrderBy(place => place.ItemId).Select(place => $"{place.ItemId}:{place.Position}"));

        return $"{teams} // {teamPlaces} // {projectPlaces}";
    }

    // US-2 · "The tree is built over matching items, and a checklist is whole": a story of Бета with two MARS tasks stands with both
    // beneath it when the filter names Бета, its card lists both in its checklist; when the filter names Альфа neither the story nor
    // its tasks are shown.
    [Fact]
    public async Task Filter_TheTreeIsBuiltOverMatchingItems_AndTheChecklistIsWhole()
    {
        var cast = await CreateAsync(factory);
        var story = await StoryAsync(factory, cast.Beta, "История Беты", cast.Mars);
        var first = await CreateAsync(factory, cast.Beta, "Задача 1", cast.Mars, "TASK", story.Id.ToString());
        var second = await CreateAsync(factory, cast.Beta, "Задача 2", cast.Mars, "TASK", story.Id.ToString());
        await StoryAsync(factory, cast.Alpha, "История Альфы", cast.Mars);

        var inBeta = await GetBacklogAsync(factory, cast, cast.Mars, query: Filter(cast.Beta.ProjectId));
        var boardInBeta = await GetBoardAsync(factory, cast, cast.Mars, query: Filter(cast.Beta.ProjectId));
        var inAlpha = await GetBacklogAsync(factory, cast, cast.Mars, query: Filter(cast.Alpha.ProjectId));
        var boardInAlpha = await GetBoardAsync(factory, cast, cast.Mars, query: Filter(cast.Alpha.ProjectId));

        Assert.Equal(
            [first.Identifier, second.Identifier],
            BacklogTreeTestData.Row(inBeta.Body.GetProperty("items").EnumerateArray(), story.Identifier).GetProperty("children").EnumerateArray()
                .Select(child => child.GetProperty("identifier").GetString()));
        var card = boardInBeta.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Single(candidate => candidate.GetProperty("identifier").GetString() == story.Identifier);
        Assert.Equal([first.Identifier, second.Identifier], card.GetProperty("checklist").EnumerateArray().Select(child => child.GetProperty("identifier").GetString()));
        Assert.DoesNotContain(story.Identifier, BacklogTreeTestData.AllRows(inAlpha.Body.GetProperty("items").EnumerateArray()).Select(row => row.GetProperty("identifier").GetString()));
        Assert.DoesNotContain(first.Identifier, BacklogTreeTestData.AllRows(inAlpha.Body.GetProperty("items").EnumerateArray()).Select(row => row.GetProperty("identifier").GetString()));
        Assert.DoesNotContain(story.Identifier, boardInAlpha.Body.GetRawText(), StringComparison.Ordinal);
    }

    // US-2 · "A filtered backlog reorders against the full list" (BR-TS-7, -8): the list A1, B1, A2, B2, B3; Max, looking at Бета,
    // moves B3 directly after B1 — the filtered view lists B1, B3, B2; unfiltered A1, B1, B3, A2, B2, G1, G2; the project orders are
    // what they were. The reorder ignores a filter in its query.
    [Fact]
    public async Task Filter_AReorderRunsAgainstTheFullList_AndMovesNoProjectOrder()
    {
        var s = await SevenAsync();
        var cast = s.Cast;
        var alphaOrder = await ProjectBoardsTestData.RowsAsync(factory, cast.Alpha);
        var betaOrder = await ProjectBoardsTestData.RowsAsync(factory, cast.Beta);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{Team(cast, cast.Mars)}/backlogs/stories/reorder?{Filter(cast.Beta.ProjectId)}",
            BoardsTestData.After(s.B3, s.B1),
            OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(Ids(s.B1, s.B3, s.B2), await RowsAsync(factory, cast, cast.Mars, query: Filter(cast.Beta.ProjectId)));
        Assert.Equal(Ids(s.A1, s.B1, s.B3, s.A2, s.B2, s.G1, s.G2), await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(alphaOrder, await ProjectBoardsTestData.RowsAsync(factory, cast.Alpha));
        Assert.Equal(betaOrder, await ProjectBoardsTestData.RowsAsync(factory, cast.Beta));
    }

    // US-2 · "A value that names no linked active project selects nothing": an unknown id, "not-a-guid", the id of a project of another
    // organization, of Дельта (linked to no team) and of Альфа after it was unlinked from MARS each give no item on the backlog and the
    // board — 200, `projects` still the team's, every case alike.
    [Fact]
    public async Task Filter_ValuesThatNameNoLinkedActiveProject_SelectNothing_AllAlike()
    {
        var s = await SevenAsync();
        var cast = s.Cast;
        var zeta = await OrganizationsTestData.CreateBareProjectAsync(factory, cast.World.OtherOrganizationId, "Зета", "ZET");
        var detached = await OrganizationsTestData.CreateBareProjectAsync(factory, cast.OrganizationId, "Отвязанный", "OFF");
        await OrganizationsTestData.LinkAsync(factory, detached.Id, cast.Mars);
        await TeamTestData.UnlinkAsync(factory, cast.OrganizationId, detached.Id, cast.Mars, TeamTestData.To(null));
        object[] values = [Guid.CreateVersion7(), "not-a-guid", zeta.Id, cast.Delta.ProjectId, detached.Id, "00000000-0000-0000-0000-000000000000"];

        foreach (var value in values)
        {
            var backlog = await GetBacklogAsync(factory, cast, cast.Mars, query: Filter(value));
            var board = await GetBoardAsync(factory, cast, cast.Mars, query: Filter(value));

            Assert.Equal(HttpStatusCode.OK, backlog.Status);
            Assert.Empty(backlog.Body.GetProperty("items").EnumerateArray());
            Assert.Equal(HttpStatusCode.OK, board.Status);
            Assert.All(board.Body.GetProperty("columns").EnumerateArray(), column => Assert.Empty(column.GetProperty("items").EnumerateArray()));
            Assert.Equal(3, backlog.Body.GetProperty("projects").GetArrayLength());
        }
    }

    // US-2 · "When the address names Beta's id and an unknown id, the view shows the items of Beta": beside a valid value an invalid
    // one adds nothing; a repeated value counts once.
    [Fact]
    public async Task Filter_AValidValueBesideAnInvalidOne_ShowsTheValidOnesItems()
    {
        var s = await SevenAsync();
        var cast = s.Cast;

        Assert.Equal(Ids(s.B1, s.B2, s.B3), await RowsAsync(factory, cast, cast.Mars, query: Filter(cast.Beta.ProjectId, Guid.CreateVersion7())));
        Assert.Equal(Ids(s.B1, s.B2, s.B3), await RowsAsync(factory, cast, cast.Mars, query: Filter("junk", cast.Beta.ProjectId, cast.Beta.ProjectId)));
        Assert.Equal(Ids(s.B1, s.B2, s.B3), await CardsAsync(factory, cast, cast.Mars, query: Filter(cast.Beta.ProjectId, "junk")));
    }

    // US-2 · "A deactivated project leaves the options and its value selects nothing": a view filtered to Гамма; Гамма is deactivated
    // — it is no longer offered and the view shows nothing; restored, the same address shows Гамма's two again.
    [Fact]
    public async Task Filter_ADeactivatedProjectLeavesTheOptions_AndItsValueSelectsNothingUntilTheRestore()
    {
        var s = await SevenAsync();
        var cast = s.Cast;
        var gamma = Filter(cast.Gamma.ProjectId);

        await DeactivateAsync(factory, cast, cast.Gamma);
        var whileDeactivated = await GetBacklogAsync(factory, cast, cast.Mars, query: gamma);
        await RestoreAsync(factory, cast, cast.Gamma);

        Assert.Empty(whileDeactivated.Body.GetProperty("items").EnumerateArray());
        Assert.Equal([Wire(cast.Alpha, "Альфа"), Wire(cast.Beta, "Бета")], ProjectsOf(whileDeactivated));
        Assert.Equal(Ids(s.G1, s.G2), await RowsAsync(factory, cast, cast.Mars, query: gamma));
    }

    // US-2 · "VENUS's filter offers its one project": VENUS's backlog and board name Альфа alone, and a filter naming Бета shows nothing.
    [Fact]
    public async Task Filter_ATeamOfOneProject_OffersThatProjectAlone_AndAForeignProjectsValueShowsNothing()
    {
        var cast = await CreateAsync(factory);
        var story = await StoryAsync(factory, cast.Alpha, "Венерина", cast.Venus);

        Assert.Equal([story.Identifier], await RowsAsync(factory, cast, cast.Venus, query: Filter(cast.Alpha.ProjectId)));
        Assert.Empty(await RowsAsync(factory, cast, cast.Venus, query: Filter(cast.Beta.ProjectId)));
        Assert.Equal([Wire(cast.Alpha, "Альфа")], ProjectsOf(await GetBoardAsync(factory, cast, cast.Venus, query: Filter(cast.Beta.ProjectId))));
    }
}
