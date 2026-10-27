using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// An item on a team view is read and written in its own project (feat-040 api-contract § 4.2, § 7; US-4; BR-TS-4, BR-TS-5): the
/// panel sends every card read, move, child creation and parent write to the project the row or card carries (<c>project.id</c>) — or,
/// for a card opened by address, to the project whose key the identifier carries, found among <c>GET /projects</c> — never to the
/// page's. Two items with one number in two projects are never mixed up; an identifier that names no readable item is a not-found
/// frame; a child lands in its parent's project with its next number and team; a parent of another project is refused. The team
/// views' own rows are <c>TeamBacklogAcrossProjectsEndpointTests</c>'.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamCardAndMovesEndpointTests(MustaskApiFactory factory)
{
    private sealed record Pair(Cast Cast, Item Alpha, Item Beta);

    /// <summary>MARS owns ALP-US-1 and BET-US-1: one number, two projects.</summary>
    private async Task<Pair> PairAsync()
    {
        var cast = await CreateAsync(factory);
        var alpha = await StoryAsync(factory, cast.Alpha, "История Альфы", cast.Mars);
        var beta = await StoryAsync(factory, cast.Beta, "История Беты", cast.Mars);
        Assert.Equal(("ALP-US-1", "BET-US-1"), (alpha.Identifier, beta.Identifier));

        return new Pair(cast, alpha, beta);
    }

    // US-4 · "A card opened on a team view opens in its own project" (BR-TS-4; doc-012 US-7): BET-US-1's row carries Бета's id; the
    // card is read there with «Проект» «Бета»; a new title is written to BET-US-1 and ALP-US-1 is unchanged; the same identifier
    // number in Альфа is another item.
    [Fact]
    public async Task Card_OfBeta_IsReadAndWrittenInBeta_AlphasItemWithTheSameNumberIsUntouched()
    {
        var pair = await PairAsync();
        var (cast, beta) = (pair.Cast, pair.Beta);
        var row = Row(await GetBacklogAsync(factory, cast, cast.Mars), beta.Identifier);
        var project = row.GetProperty("project").GetProperty("id").GetGuid();
        var alphaBefore = await LinksTestData.StoredAsync(factory, pair.Alpha);

        var read = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{OrganizationsApi.Project(cast.OrganizationId, project)}/workitems/{beta.Identifier}", token: OrganizationsTokens.SignedIn());
        var saved = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Patch,
            $"{OrganizationsApi.Project(cast.OrganizationId, project)}/workitems/{beta.Identifier}",
            new { title = "Новое название" },
            OrganizationsTokens.SignedIn());

        Assert.Equal(cast.Beta.ProjectId, project);
        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal((beta.Identifier, Wire(cast.Beta, "Бета")), (read.Body.GetProperty("identifier").GetString(), ProjectOf(read.Body)));
        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal("Новое название", (await WorkItemsTestData.ReadAsync(factory, cast.Beta.Items, beta.Identifier)).GetProperty("title").GetString());
        Assert.Equal("История Альфы", (await WorkItemsTestData.ReadAsync(factory, cast.Alpha.Items, pair.Alpha.Identifier)).GetProperty("title").GetString());
        Assert.Equal(alphaBefore, await LinksTestData.StoredAsync(factory, pair.Alpha));
        Assert.Equal(Wire(cast.Alpha, "Альфа"), ProjectOf(Row(await GetBacklogAsync(factory, cast, cast.Mars), pair.Alpha.Identifier)));
    }

    // US-4 · "A card opened by address is read in the project its key names" (BR-TS-4): the panel finds the key among the
    // organization's active projects (`GET /projects`, each with `key`); BET-US-1 is read in Бета. An item not on the team's view
    // (owned by VENUS) opens too.
    [Fact]
    public async Task Card_ByAddress_IsReadInTheProjectItsKeyNames_AlsoAnItemNotOnTheView()
    {
        var pair = await PairAsync();
        var cast = pair.Cast;
        var venus = await StoryAsync(factory, cast.Alpha, "Венерина", cast.Venus);

        var projects = await TeamTestData.GetAsync(factory, $"/api/organizations/{cast.OrganizationId}/projects");
        var betaId = projects.Body.EnumerateArray().Single(project => project.GetProperty("key").GetString() == "BET").GetProperty("id").GetGuid();
        var alphaId = projects.Body.EnumerateArray().Single(project => project.GetProperty("key").GetString() == "ALP").GetProperty("id").GetGuid();
        var inBeta = await WorkItemsTestData.GetAsync(factory, cast.Beta.Items, pair.Beta.Identifier);
        var offTheView = await WorkItemsTestData.GetAsync(factory, cast.Alpha.Items, venus.Identifier);

        Assert.Equal(cast.Beta.ProjectId, betaId);
        Assert.Equal(cast.Alpha.ProjectId, alphaId);
        Assert.Equal(HttpStatusCode.OK, inBeta.Status);
        Assert.Equal(pair.Beta.Identifier, inBeta.Body.GetProperty("identifier").GetString());
        Assert.Equal(HttpStatusCode.OK, offTheView.Status);
        Assert.DoesNotContain(venus.Identifier, await RowsAsync(factory, cast, cast.Mars));
        Assert.All(projects.Body.EnumerateArray(), project => Assert.Equal(["id", "name", "key"], project.EnumerateObject().Select(member => member.Name).Take(3)));
    }

    // US-4 · "An identifier that names no readable item shows the not-found frame" (BR-TS-4): the server's refusals the frame stands on —
    // a deactivated project's key (the project is absent from the list and 404 organizations.project.not_found), another
    // organization's project (the same), an item that does not exist, a key/type mismatch (item 1 of Альфа is a story: ALP-BUG-1) and a
    // garbage identifier (404 workitems.workitem.not_found) — each a 404; none says why.
    [Fact]
    public async Task Card_AnIdentifierThatNamesNoReadableItem_IsNotFound()
    {
        var pair = await PairAsync();
        var cast = pair.Cast;
        var inGamma = await StoryAsync(factory, cast.Gamma, "Гаммы", cast.Mars);
        await DeactivateAsync(factory, cast, cast.Gamma);
        var foreign = await BoardsTestData.CreateWorldAsync(factory);
        var projects = await TeamTestData.GetAsync(factory, $"/api/organizations/{cast.OrganizationId}/projects");

        var deactivated = await WorkItemsTestData.GetAsync(factory, cast.Gamma.Items, inGamma.Identifier);
        var otherOrganization = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Project(cast.OrganizationId, foreign.ProjectId)}/workitems/MSK-US-1",
            token: OrganizationsTokens.SignedIn());
        var missing = await WorkItemsTestData.GetAsync(factory, cast.Alpha.Items, "ALP-US-999");
        var mismatch = await WorkItemsTestData.GetAsync(factory, cast.Alpha.Items, "ALP-BUG-1");
        var garbage = await WorkItemsTestData.GetAsync(factory, cast.Alpha.Items, "garbage");

        Assert.DoesNotContain(projects.Body.EnumerateArray(), project => project.GetProperty("key").GetString() == "GAM");
        Assert.DoesNotContain(projects.Body.EnumerateArray(), project => project.GetProperty("key").GetString() == "MSK");
        BoardsTestData.AssertRefused(deactivated, HttpStatusCode.NotFound, "organizations.project.not_found");
        BoardsTestData.AssertRefused(otherOrganization, HttpStatusCode.NotFound, "organizations.project.not_found");
        BoardsTestData.AssertRefused(missing, HttpStatusCode.NotFound, "workitems.workitem.not_found");
        BoardsTestData.AssertRefused(mismatch, HttpStatusCode.NotFound, "workitems.workitem.not_found");
        BoardsTestData.AssertRefused(garbage, HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // US-4 · "Project views read the card in the page's project, as before" (feat-028's re-check): Бета's identifier in Альфа's
    // address is not found, as today.
    [Fact]
    public async Task Card_AnotherProjectsIdentifierAtThisProjectsAddress_IsNotFound()
    {
        var pair = await PairAsync();

        var answer = await WorkItemsTestData.GetAsync(factory, pair.Cast.Alpha.Items, "BET-US-1");

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // US-4 · "A card move writes the item's state in its own project" (BR-TS-5; doc-012 US-7): BET-US-1 «Новая» → «Активна» — in Бета,
    // owned by MARS, with its identifier and both backlog places unchanged; the next read of MARS's and of Бета's backlogs and boards
    // shows it once, in «Активна».
    [Fact]
    public async Task Move_WritesTheStateInTheItemsProject_NothingElse_PlacesUnchanged()
    {
        var pair = await PairAsync();
        var (cast, beta) = (pair.Cast, pair.Beta);
        await OrderAsync(factory, cast, cast.Mars, pair.Alpha, beta);
        await ProjectBoardsTestData.MoveAsync(factory, cast.Beta, BoardsTestData.Top(beta));
        var teamPlaces = await TeamPlacesOfAsync(factory, beta.Id);
        var projectPlaces = await ProjectPlacesOfAsync(factory, beta.Id);

        var answer = await BoardsTestData.SetStateAsync(factory, cast.Beta, beta, "US", "Активна");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Активна", answer.Body.GetProperty("state").GetProperty("name").GetString());
        Assert.Equal((cast.Beta.ProjectId, beta.Identifier), (answer.Body.GetProperty("projectId").GetGuid(), answer.Body.GetProperty("identifier").GetString()));
        Assert.Equal(cast.Mars, (await StoredAsync(factory, cast.Beta.ProjectId, beta.Identifier)).TeamId);
        Assert.Equal((teamPlaces, projectPlaces), (await TeamPlacesOfAsync(factory, beta.Id), await ProjectPlacesOfAsync(factory, beta.Id)));
        foreach (var board in new[] { await GetBoardAsync(factory, cast, cast.Mars), await ProjectBoardsTestData.GetBoardAsync(factory, cast.Beta) })
        {
            Assert.Equal(["Активна: BET-US-1"], BoardsTestData.Columns(board.Body).Where(column => column.Contains("BET-US-1", StringComparison.Ordinal)));
        }

        Assert.Single(await RowsAsync(factory, cast, cast.Mars), beta.Identifier);
        Assert.Single(await ProjectBoardsTestData.RowsAsync(factory, cast.Beta), beta.Identifier);
    }

    // US-4 · "A move offers only the states of the card's own type" (BR-TS-5; BR-MX-19; mod-006 BR-M4-18): the read changes no
    // item's state; a direct write of a state of another type (a bug's) onto a story is refused 409 «Этот статус недоступен для
    // рабочего элемента этого типа.» and nothing changes.
    [Fact]
    public async Task Move_ToAStateOfAnotherType_IsRefused_NothingChanges()
    {
        var pair = await PairAsync();
        var (cast, beta) = (pair.Cast, pair.Beta);
        var before = await LinksTestData.StoredAsync(factory, beta);
        await GetBoardAsync(factory, cast, cast.Mars);

        var answer = await WorkItemsTestData.SaveAsync(
            factory, cast.Beta.Items, beta.Identifier, new { stateId = cast.Beta.Items.Types["BUG"].StateId("Закрыта").ToString() });

        BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.workitem.state_not_allowed");
        Assert.Equal("Этот статус недоступен для рабочего элемента этого типа.", answer.Detail);
        Assert.Equal(before, await LinksTestData.StoredAsync(factory, beta));
    }

    // US-4 · "A child added from a row or a card lands in the parent's project with its team" (BR-TS-5; mod-017 BR-OT-3): the row's
    // «+» and the card's «Добавить задачу» are mod-003's create with `parentId`, sent to the parent's project: both tasks are created
    // in Бета with Бета's next numbers, the story as parent and MARS as team; both stand beneath it on MARS's backlog and in its
    // checklist.
    [Fact]
    public async Task Child_AddedBeneathAnItemOfBeta_LandsInBetaWithItsNumberAndTheParentsTeam()
    {
        var pair = await PairAsync();
        var (cast, beta) = (pair.Cast, pair.Beta);

        var first = await CreateAsync(factory, cast.Beta, "Задача раз", null, "TASK", beta.Id.ToString());
        var second = await CreateAsync(factory, cast.Beta, "Задача два", null, "TASK", beta.Id.ToString());

        Assert.Equal(["BET-TASK-2", "BET-TASK-3"], new[] { first.Identifier, second.Identifier });
        Assert.All(new[] { first, second }, child => Assert.Equal(cast.Mars, StoredAsync(factory, cast.Beta.ProjectId, child.Identifier).GetAwaiter().GetResult().TeamId));
        var backlog = await GetBacklogAsync(factory, cast, cast.Mars);
        Assert.Equal(
            [first.Identifier, second.Identifier],
            BacklogTreeTestData.Row(backlog.Body.GetProperty("items").EnumerateArray(), beta.Identifier).GetProperty("children").EnumerateArray()
                .Select(child => child.GetProperty("identifier").GetString()));
        var board = await GetBoardAsync(factory, cast, cast.Mars);
        var card = board.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Single(candidate => candidate.GetProperty("identifier").GetString() == beta.Identifier);
        Assert.Equal([first.Identifier, second.Identifier], card.GetProperty("checklist").EnumerateArray().Select(child => child.GetProperty("identifier").GetString()));
        Assert.Equal(["ALP-US-1"], await ProjectBoardsTestData.RowsAsync(factory, cast.Alpha));
    }

    // US-4 · "A row dropped onto a row of another project is a refused target" (BR-TS-5; feat-027 BR-T-12): a direct parent write
    // naming ALP-US-1 as BET-US-1's parent answers 404 «Родитель не найден в этом проекте.» and nothing changes; the picker of Бета
    // offers parents of Бета only.
    [Fact]
    public async Task Parent_OfAnotherProject_Is404ParentNotFound_AndThePickerOffersTheItemsOwnProjectOnly()
    {
        var pair = await PairAsync();
        var (cast, alpha, beta) = (pair.Cast, pair.Alpha, pair.Beta);
        var feature = await CreateAsync(factory, cast.Beta, "Фича Беты", cast.Mars, "FEAT");
        var alphaFeature = await CreateAsync(factory, cast.Alpha, "Фича Альфы", cast.Mars, "FEAT");
        var before = await LinksTestData.StoredAsync(factory, beta);

        var answer = await LinksTestData.SetParentAsync(factory, cast.Beta.Items, beta.Identifier, alphaFeature.Id, null);
        var offered = await LinksTestData.OfferedAsync(factory, cast.Beta.Items, beta.Identifier);

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, "workitems.link.parent_not_found");
        Assert.Equal("Родитель не найден в этом проекте.", answer.Detail);
        Assert.Equal(before, await LinksTestData.StoredAsync(factory, beta));
        Assert.Contains(feature.Identifier, offered);
        Assert.DoesNotContain(alphaFeature.Identifier, offered);
        Assert.DoesNotContain(alpha.Identifier, offered);
    }

    // US-4 · "«Читатель» reads cards and cannot change them" (BR-TS-13): Vera reads MARS's board and BET-US-1's card; her direct
    // state write and her direct child creation are 403 «Недостаточно прав», and nothing changes.
    [Fact]
    public async Task Viewer_ReadsTheCard_ButCannotMoveItOrAddAChild()
    {
        var pair = await PairAsync();
        var (cast, beta) = (pair.Cast, pair.Beta);
        var vera = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Viewer);
        var before = await LinksTestData.StoredAsync(factory, beta);

        var board = await GetBoardAsync(factory, cast, cast.Mars, token: vera.Token);
        var read = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, WorkItemsTestData.Item(cast.Beta.Items, beta.Identifier), token: vera.Token);
        var move = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Patch,
            WorkItemsTestData.Item(cast.Beta.Items, beta.Identifier),
            new { stateId = cast.Beta.Items.Types["US"].StateId("Активна").ToString() },
            vera.Token);
        var child = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(cast.Beta.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = cast.Beta.Items.Types["TASK"].Id.ToString(), title = "Задача", parentId = beta.Id.ToString() },
            vera.Token);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (board.Status, read.Status));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (move.Status, move.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (child.Status, child.ErrorCode));
        Assert.Equal(before, await LinksTestData.StoredAsync(factory, beta));
        Assert.Single(await WorkItemsTestData.StoredAsync(factory, cast.Beta.ProjectId));
    }
}
