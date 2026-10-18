using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// QA of feat-040 beyond the developer's tests (qa.md, Backend): what the contract promises and no test pinned. The item's
/// <c>project</c> on every answer that returns a work item (api-contract § 4.2: create, parent write, restore — the developer's
/// test reads the get and the save), and the project filter's value forms a hand-edited address can carry (§ 3.1: "each value is
/// a canonical project id or is ignored", none an error).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamViewQaEndpointTests(MustaskApiFactory factory)
{
    // api-contract § 4.2 · "It rides every answer that returns a WorkItem": an item of Бета (not the cast's first project) carries
    // Бета's {id, name} on the answer of its creation, of its deletion and of its restore. (The parent write answers
    // `{ parent }`, a WorkItemRef, not a WorkItem — § 4.2 names it among the answers, which it is not: qa.md, defect D-B1.)
    [Fact]
    public async Task WorkItem_ProjectRidesTheAnswersOfCreateDeleteAndRestore_ItIsTheItemsOwn()
    {
        var cast = await CreateAsync(factory);

        var created = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(cast.Beta.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = cast.Beta.Items.Types["US"].Id.ToString(),
                title = "История Беты",
                team = new { teamId = cast.Mars.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var story = new Item(created.Body.GetProperty("id").GetGuid(), created.Body.GetProperty("identifier").GetString()!);

        var deleted = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{WorkItemsTestData.Item(cast.Beta.Items, story.Identifier)}/delete",
            token: OrganizationsTokens.SignedIn());
        var restored = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{WorkItemsTestData.Item(cast.Beta.Items, story.Identifier)}/restore",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Answer[] answers = [created, deleted, restored];
        Assert.All(answers, answer => Assert.Equal(Wire(cast.Beta, "Бета"), ProjectOf(answer.Body)));
        Assert.All(answers, answer => Assert.Equal(cast.Beta.ProjectId, answer.Body.GetProperty("projectId").GetGuid()));
    }

    // api-contract § 3.1 · "Each value is a canonical project id (either letter case, not all zeros) or is ignored; a filter that is set
    // but names no project the team's items are in selects nothing, and none is an error": the odd forms an address can carry — a
    // blank value, braces, no hyphens, spaces, a very long one — are 200 and select nothing beside no valid value, and beside a
    // valid one add nothing; the same on the board.
    [Fact]
    public async Task Filter_OddValueForms_AreNeverAnError_SelectNothingAlone_AndAddNothingBesideAValidOne()
    {
        var cast = await CreateAsync(factory);
        var alpha = await StoryAsync(factory, cast.Alpha, "Альфы", cast.Mars);
        var beta = await StoryAsync(factory, cast.Beta, "Беты", cast.Mars);
        var betaId = cast.Beta.ProjectId;
        string[] odd =
        [
            string.Empty,
            "%20",
            Uri.EscapeDataString("{" + betaId + "}"),
            betaId.ToString("N"),
            Uri.EscapeDataString(" " + betaId),
            "%00",
            "1",
            new string('9', 300),
        ];

        foreach (var value in odd)
        {
            var alone = $"projectId={value}";
            var backlog = await GetBacklogAsync(factory, cast, cast.Mars, query: alone);
            var board = await GetBoardAsync(factory, cast, cast.Mars, query: alone);

            Assert.True(backlog.Status == HttpStatusCode.OK, $"A backlog filtered by «{value}» answered {backlog.Status} {backlog.ErrorCode}.");
            Assert.True(board.Status == HttpStatusCode.OK, $"A board filtered by «{value}» answered {board.Status} {board.ErrorCode}.");
            Assert.Empty(backlog.Body.GetProperty("items").EnumerateArray());
            Assert.All(board.Body.GetProperty("columns").EnumerateArray(), column => Assert.Empty(column.GetProperty("items").EnumerateArray()));
            Assert.Equal(3, backlog.Body.GetProperty("projects").GetArrayLength());

            Assert.Equal([beta.Identifier], await RowsAsync(factory, cast, cast.Mars, query: $"{alone}&projectId={betaId}"));
            Assert.Equal([beta.Identifier], await CardsAsync(factory, cast, cast.Mars, query: $"projectId={betaId}&{alone}"));
        }

        Assert.Equal([alpha.Identifier, beta.Identifier], await RowsAsync(factory, cast, cast.Mars));
    }

    // api-contract § 2.2 · "`projects` … by name in Russian collation (letter case not deciding), then id": Бета renamed «аббат» (a
    // lower-case first letter) stands before «Альфа» — an ordinal order would put every capital first.
    [Fact]
    public async Task Projects_AreByNameInRussianCollation_LetterCaseNotDeciding()
    {
        var cast = await CreateAsync(factory);
        var renamed = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Project(cast.OrganizationId, cast.Beta.ProjectId)}/name",
            new { name = "аббат" },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, renamed.Status);

        var backlog = await GetBacklogAsync(factory, cast, cast.Mars);
        var board = await GetBoardAsync(factory, cast, cast.Mars);

        string[] expected = [Wire(cast.Beta, "аббат"), Wire(cast.Alpha, "Альфа"), Wire(cast.Gamma, "Гамма")];
        Assert.Equal(expected, ProjectsOf(backlog));
        Assert.Equal(expected, ProjectsOf(board));
    }

    // api-contract § 2.3 · "on a board card `order` is the item's position in the whole order of the level": three stories of three
    // projects ordered G, A, B, with B moved to «Активна» — its card is in another column and still reads order 3.
    [Fact]
    public async Task BoardCard_Order_IsThePositionInTheWholeOrderOfTheLevel_NotInItsColumn()
    {
        var cast = await CreateAsync(factory);
        var a = await StoryAsync(factory, cast.Alpha, "A", cast.Mars);
        var b = await StoryAsync(factory, cast.Beta, "B", cast.Mars);
        var g = await StoryAsync(factory, cast.Gamma, "G", cast.Mars);
        await OrderAsync(factory, cast, cast.Mars, g, a, b);
        var moved = await BoardsTestData.SetStateAsync(factory, cast.Beta, b, "US", "Активна");
        Assert.Equal(HttpStatusCode.OK, moved.Status);

        var board = await GetBoardAsync(factory, cast, cast.Mars);

        var columns = board.Body.GetProperty("columns").EnumerateArray().ToList();
        var cards = columns
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .ToDictionary(card => card.GetProperty("identifier").GetString()!, card => card.GetProperty("order").GetInt32());
        Assert.Equal(
            new Dictionary<string, int> { [g.Identifier] = 1, [a.Identifier] = 2, [b.Identifier] = 3 }.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            cards.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.True(columns.Count(column => column.GetProperty("items").GetArrayLength() > 0) >= 2);
    }

    // api-contract § 3.1 · a filter naming every linked project at once is the whole view, and a long run of repeated values (a
    // hand-built address) is one statement's list, not an error: 120 values, the three projects among them, shows all three projects'
    // items in the team's order.
    [Fact]
    public async Task Filter_ManyRepeatedValues_ShowTheNamedProjectsItems_InTheTeamsOrder()
    {
        var cast = await CreateAsync(factory);
        var alpha = await StoryAsync(factory, cast.Alpha, "Альфы", cast.Mars);
        var beta = await StoryAsync(factory, cast.Beta, "Беты", cast.Mars);
        var gamma = await StoryAsync(factory, cast.Gamma, "Гаммы", cast.Mars);
        var noise = Enumerable.Range(0, 117).Select(_ => (object)Guid.CreateVersion7());
        object[] values = [.. noise, cast.Gamma.ProjectId, cast.Alpha.ProjectId, cast.Beta.ProjectId];

        var rows = await RowsAsync(factory, cast, cast.Mars, query: Filter(values));
        var cards = await CardsAsync(factory, cast, cast.Mars, query: Filter(values));

        Assert.Equal([alpha.Identifier, beta.Identifier, gamma.Identifier], rows);
        Assert.Equal([alpha.Identifier, beta.Identifier, gamma.Identifier], cards.Order(StringComparer.Ordinal));
    }
}
