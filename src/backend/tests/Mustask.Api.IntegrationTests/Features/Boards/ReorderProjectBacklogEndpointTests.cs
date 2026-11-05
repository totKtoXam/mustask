using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/backlogs/{level}/reorder</c> (feat-039 api-contract
/// § 3.2): US-2 — one item to the top, the bottom or after another, in an order that belongs to the project and touches no
/// team's; reorders of one project backlog one at a time in arrival order; a stale move refused; who may reorder; the order
/// of answers.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ReorderProjectBacklogEndpointTests(MustaskApiFactory factory)
{
    /// <summary>US-2's data: «Истории» of the project read S1, S2, S3 (S1 and S2 on «Мастаск»'s backlog, S3 on «Платформа»'s), a bug B4 after them.</summary>
    private async Task<(BoardsWorld World, Item S1, Item S2, Item S3, Item B4)> ProjectAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var s1 = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.RootAreaId);
        var s2 = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.RootAreaId);
        var s3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        var b4 = await BoardsTestData.CreateAsync(factory, world, "BUG", "Баг", world.PlatformAreaId);

        return (world, s1, s2, s3, b4);
    }

    private static string[] Ids(params Item[] items) => items.Select(item => item.Identifier).ToArray();

    // --- The moves --------------------------------------------------------------------------------------------------

    // US-2 · "A reorder by keyboard / by pointer": S3 to the top reads S3, S1, S2, B4 with «Порядок» 1..4, at every later read.
    [Fact]
    public async Task Reorder_Top_Returns204_OrderPersists()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();

        var answer = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s3));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(Ids(s3, s1, s2, b4), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(Ids(s3, s1, s2, b4), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-2 · "A reorder by keyboard": the entries of the row's menu map to top, after the row above two up, after the row below
    // and bottom (mod-006 api-contract § 3.2's mapping), on S2, the second row.
    [Theory]
    [InlineData("В начало", "S2,S1,S3,B4")]
    [InlineData("Ниже", "S1,S3,S2,B4")]
    [InlineData("В конец", "S1,S3,B4,S2")]
    public async Task Reorder_KeyboardEntries_MoveTheSecondRow(string entry, string expected)
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();
        var byName = new Dictionary<string, Item> { ["S1"] = s1, ["S2"] = s2, ["S3"] = s3, ["B4"] = b4 };

        var body = entry switch
        {
            "В начало" => BoardsTestData.Top(s2),
            "Ниже" => BoardsTestData.After(s2, s3),
            _ => BoardsTestData.Bottom(s2),
        };

        await ProjectBoardsTestData.MoveAsync(factory, world, body);

        Assert.Equal(expected.Split(',').Select(name => byName[name].Identifier), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-2 · "A reorder by pointer": dropped between two rows is "directly after" the one above; after itself nothing moves.
    [Fact]
    public async Task Reorder_AfterAnItem_AndAfterItself()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.After(b4, s1));
        Assert.Equal(Ids(s1, b4, s2, s3), await ProjectBoardsTestData.RowsAsync(factory, world));

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.After(s2, s2));
        Assert.Equal(Ids(s1, b4, s2, s3), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(4, (await ProjectBoardsTestData.PlacesAsync(factory, world)).Count);
    }

    // US-2 · "A reorder by keyboard": reordering changes no work item — its fields and «Изменён» stay, as does the list's order.
    [Fact]
    public async Task Reorder_ChangesNoWorkItem()
    {
        var (world, _, _, s3, _) = await ProjectAsync();
        var storedBefore = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var listBefore = (await WorkItemsTestData.ListAsync(factory, world.Items)).Body.GetRawText();

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));

        Assert.Equal(storedBefore, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        Assert.Equal(listBefore, (await WorkItemsTestData.ListAsync(factory, world.Items)).Body.GetRawText());
    }

    // US-2 · reorder moves a row with its subtree: the order is of the top-level rows, the nested ones go with their parent, and a
    // reorder places every item of the level, nested items included.
    [Fact]
    public async Task Reorder_ARowWithItsSubtree_MovesTogether_AndPlacesTheNestedItemsToo()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var parent = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Родитель");
        var child = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Дочерняя", world.PlatformAreaId);
        var other = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Другая");
        await LinksTestData.LinkAsync(factory, world.Items, child, parent);

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(other), "features");

        Assert.Equal($"{other.Identifier} {parent.Identifier}[{child.Identifier}]", await ProjectBoardsTestData.ShapeAsync(factory, world, "features"));
        Assert.Equal(3, (await ProjectBoardsTestData.PlacesAsync(factory, world)).Count);
    }

    // The epics backlog orders its own items, apart from the stories' (BR-PB-1: one backlog per project and level).
    [Fact]
    public async Task Reorder_EpicsBacklog_IsItsOwnOrder()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();
        var first = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Первый эпик", world.PlatformAreaId);
        var second = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Второй эпик");

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(second), "epics");

        Assert.Equal(Ids(second, first), await ProjectBoardsTestData.RowsAsync(factory, world, "epics"));
        Assert.Equal(Ids(s1, s2, s3, b4), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // --- The project's order and the teams' ------------------------------------------------------------------------

    // US-2 · "The project's order and a team's order are independent" (BR-MX-18, ADR-0028 rule 9): S3 to the top of the project
    // leaves «Мастаск»'s S1, S2; S2 to the top of «Мастаск»'s then leaves the project's S3, S1, S2; and on each board a column
    // lists its cards in its own view's order.
    [Fact]
    public async Task Reorder_TheProjectsOrderAndATeamsOrder_AreIndependent()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));

        Assert.Equal(Ids(s3, s1, s2, b4), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(Ids(s1, s2), await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
        Assert.Equal(Ids(s3, b4), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));

        await BoardsTestData.MoveAsync(factory, world, world.ProjectTeamId, BoardsTestData.Top(s2));

        Assert.Equal(Ids(s2, s1), await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
        Assert.Equal(Ids(s3, s1, s2, b4), await ProjectBoardsTestData.RowsAsync(factory, world));

        var project = await ProjectBoardsTestData.GetBoardAsync(factory, world);
        var team = await BoardsTestData.GetBoardAsync(factory, world, world.ProjectTeamId);
        Assert.Equal(Ids(s3, s1, s2, b4), BoardsTestData.Cards(BoardsTestData.Column(project.Body, "Новая")));
        Assert.Equal(Ids(s2, s1), BoardsTestData.Cards(BoardsTestData.Column(team.Body, "Новая")));
    }

    // US-2 · same, at the tables: a project reorder writes project places only and a team reorder team places only, and an item
    // that holds both keeps both.
    [Fact]
    public async Task Reorder_WritesOnlyItsOwnPlaces()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();
        await BoardsTestData.MoveAsync(factory, world, world.ProjectTeamId, BoardsTestData.Top(s2));
        var teamBefore = await ProjectBoardsTestData.TeamPlacesAsync(factory, world);
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Bottom(s1));

        Assert.Equal(teamBefore, await ProjectBoardsTestData.TeamPlacesAsync(factory, world));
        var projectBefore = await ProjectBoardsTestData.PlacesAsync(factory, world);
        Assert.Equal(Ids(s2, s3, b4, s1), ProjectBoardsTestData.ByPosition(projectBefore, s1, s2, s3, b4));

        await BoardsTestData.MoveAsync(factory, world, world.ProjectTeamId, BoardsTestData.Bottom(s2));

        Assert.Equal(projectBefore, await ProjectBoardsTestData.PlacesAsync(factory, world));
        Assert.Contains(await ProjectBoardsTestData.TeamPlacesAsync(factory, world), place => place.ItemId == s1.Id);
    }

    // US-2 · "An item that changes team keeps its place on the project's backlog" (BR-MX-18; here by the item's «Область»): S1
    // leaves «Мастаск»'s backlog for «Платформа»'s and is still second on the project's.
    [Fact]
    public async Task Reorder_AnItemThatChangesTeam_KeepsItsProjectPlace()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s2));
        await BoardsTestData.MoveAsync(factory, world, world.ProjectTeamId, BoardsTestData.Bottom(s2));
        var placeBefore = (await ProjectBoardsTestData.PlacesAsync(factory, world)).Single(place => place.ItemId == s1.Id);

        await BoardsTestData.MoveToAreaAsync(factory, world, s1, world.PlatformAreaId);

        Assert.Equal(Ids(s2, s1, s3, b4), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(placeBefore, (await ProjectBoardsTestData.PlacesAsync(factory, world)).Single(place => place.ItemId == s1.Id));
        Assert.DoesNotContain(s1.Identifier, await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
        Assert.Contains(s1.Identifier, await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-2 · "New and unplaced items follow the placed ones": a story created afterwards from «Мастаск»'s backlog is the last row.
    [Fact]
    public async Task Reorder_ThenACreatedItem_IsTheLastRow_AndHoldsNoPlace()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(b4));

        var created = await BoardsTestData.CreateFromTeamAsync(factory, world, "US", "Новая история", world.ProjectTeamId.ToString());

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var identifier = created.Body.GetProperty("identifier").GetString()!;
        Assert.Equal(Ids(b4, s1, s2, s3).Append(identifier).ToArray(), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(4, (await ProjectBoardsTestData.PlacesAsync(factory, world)).Count);
    }

    // mod-006 BR-M4-24 on the project: a reorder places every item of the backlog and drops the places of items that left it
    // (here, entered the Removed category); the place of one that left is kept until then.
    [Fact]
    public async Task Reorder_PlacesEveryItem_DropsThePlacesOfItemsThatLeft()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));
        Assert.Equal(Ids(s3, s1, s2, b4), ProjectBoardsTestData.ByPosition(await ProjectBoardsTestData.PlacesAsync(factory, world), s1, s2, s3, b4));

        await LinksTestData.DeleteAsync(factory, world.Items, s2);
        Assert.Contains(await ProjectBoardsTestData.PlacesAsync(factory, world), place => place.ItemId == s2.Id);

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Bottom(s3));

        var places = await ProjectBoardsTestData.PlacesAsync(factory, world);
        Assert.Equal(Ids(s1, b4, s3), ProjectBoardsTestData.ByPosition(places, s1, s2, s3, b4));
        Assert.DoesNotContain(places, place => place.ItemId == s2.Id);
    }

    // --- Refused ----------------------------------------------------------------------------------------------------

    // US-2 · "A reorder naming an item that is not on this backlog is refused" (mod-006 BR-M4-26): an item of another project, an
    // epic on the «Истории» backlog, an item in the Removed category, and an anchor that is any of them — 409, no place changes.
    [Fact]
    public async Task Reorder_ItemsThatAreNotOnThisBacklog_Return409Stale_NothingChanges()
    {
        var (world, s1, _, _, _) = await ProjectAsync();
        var other = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "OTH", "Другой проект");
        var foreignAnswer = await WorkItemsTestData.CreateAsync(factory, other, "US", "Чужая");
        var foreign = new Item(foreignAnswer.Body.GetProperty("id").GetGuid(), foreignAnswer.Body.GetProperty("identifier").GetString()!);
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик");
        var removed = await BoardsTestData.CreateAsync(factory, world, "US", "Удалённая");
        await LinksTestData.DeleteAsync(factory, world.Items, removed);
        const string Stale = "boards.backlog.reorder_stale";

        var anchorForeign = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.After(s1, foreign));
        var movedForeign = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(foreign));
        var movedEpic = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(epic));
        var anchorEpic = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.After(s1, epic));
        var movedRemoved = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(removed));
        var anchorRemoved = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.After(s1, removed));

        foreach (var answer in new[] { anchorForeign, movedForeign, movedEpic, anchorEpic, movedRemoved, anchorRemoved })
        {
            BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, Stale);
            Assert.Equal("Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.", answer.Detail);
        }

        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));
        Assert.Empty(await ProjectBoardsTestData.TeamPlacesAsync(factory, world));
    }

    // api-contract § 3.2 step 3: a missing, malformed or unknown id names no item of this backlog — stale, nothing changes.
    [Theory]
    [InlineData(null, "top", null)]
    [InlineData("not-an-id", "top", null)]
    [InlineData("unknown", "top", null)]
    [InlineData("s1", "after", null)]
    [InlineData("s1", "after", "not-an-id")]
    [InlineData("s1", "after", "unknown")]
    public async Task Reorder_ItemOrAnchorNamingNothing_Returns409Stale(string? item, string position, string? after)
    {
        var (world, s1, _, _, _) = await ProjectAsync();

        string? Id(string? value) => value switch
        {
            "s1" => s1.Id.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => value,
        };

        var answer = await ProjectBoardsTestData.ReorderAsync(factory, world, new { itemId = Id(item), position, afterItemId = Id(after) });

        BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));
    }

    // api-contract § 3.2: a position that is none of the three words is a panel defect, 400 with the field's reason.
    [Theory]
    [InlineData(null)]
    [InlineData("sideways")]
    [InlineData("TOP")]
    public async Task Reorder_InvalidPosition_Returns400(string? position)
    {
        var (world, s1, _, _, _) = await ProjectAsync();

        var answer = await ProjectBoardsTestData.ReorderAsync(factory, world, new { itemId = s1.Id.ToString(), position });

        BoardsTestData.AssertRefused(answer, HttpStatusCode.BadRequest, "boards.validation_failed");
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("position").EnumerateArray());
        Assert.Equal(("boards.backlog.position_invalid", "Некорректная позиция перемещения."), (
            reason.GetProperty("code").GetString(), reason.GetProperty("detail").GetString()));
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));
    }

    // --- Concurrency ------------------------------------------------------------------------------------------------

    // US-2 · "Two users reorder at once" (mod-006 BR-M4-25): U's reorder holds the project backlog's lock at its commit; U2's
    // reorder of another item waits on it, then reads the order U committed and moves only its own item. Both answer 204.
    [Fact]
    public async Task Reorder_DifferentItemsAtTheSameMoment_SecondWaits_BothCount()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();

        using var gate = factory.CommitGates.Open();
        var first = ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Bottom(s1), gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first reorder finished before reaching its commit gate.");

        var second = ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s3));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second reorder did not wait on the project backlog's lock.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (a.Status, b.Status));
        Assert.Equal(Ids(s3, s2, b4, s1), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-2 · same: the same item moved by both, the later arrival decides its place and nobody is told.
    [Fact]
    public async Task Reorder_SameItemAtTheSameMoment_TheLaterDecides()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();

        using var gate = factory.CommitGates.Open();
        var first = ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s2), gate: gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first reorder finished before reaching its commit gate.");

        var second = ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Bottom(s2));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (a.Status, b.Status));
        Assert.Equal(Ids(s1, s3, b4, s2), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-2 · "a reorder of MARS's backlog at the same moment fails neither request" (ADR-0028 rule 9): the project reorder holds
    // its lock at its commit, and a reorder of «Мастаск»'s own backlog goes through without waiting for it.
    [Fact]
    public async Task Reorder_AProjectReorderAndATeamReorderAtTheSameMoment_FailNeither()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();

        using var gate = factory.CommitGates.Open();
        var project = ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s3), gate: gate);
        await Task.WhenAny(gate.Reached, project);
        Assert.False(project.IsCompleted, "The project reorder finished before reaching its commit gate.");

        var team = await BoardsTestData.ReorderAsync(factory, world, world.ProjectTeamId, BoardsTestData.Top(s2));

        Assert.Equal(HttpStatusCode.NoContent, team.Status);
        Assert.False(project.IsCompleted, "The project reorder finished while its gate was closed.");

        gate.Release();

        Assert.Equal(HttpStatusCode.NoContent, (await project).Status);
        Assert.Equal(Ids(s3, s1, s2, b4), await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(Ids(s2, s1), await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
    }

    // ADR-0026 / api-contract § 0.4 step 3: the project deactivated while the reorder waited for its lock — the deactivation holds
    // the project at its commit, the reorder waits on it, then answers the project's not-found and places nothing.
    [Fact]
    public async Task Reorder_ProjectDeactivatedWhileWaiting_Returns404ProjectNotFound()
    {
        var (world, _, _, s3, _) = await ProjectAsync();

        using var gate = factory.CommitGates.Open();
        var deactivation = OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{ProjectBoardsTestData.Project(world)}/deactivate", token: OrganizationsTokens.SignedIn(), gate: gate);
        await Task.WhenAny(gate.Reached, deactivation);
        Assert.False(deactivation.IsCompleted, "The deactivation finished before reaching its commit gate.");

        // The address check reads the committed project, active; the lock then waits for the deactivation.
        var reorder = ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s3));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, reorder);
        Assert.False(reorder.IsCompleted, "The reorder did not wait on the project's lock.");

        gate.Release();
        Assert.True((await deactivation).Status is HttpStatusCode.OK or HttpStatusCode.NoContent);
        BoardsTestData.AssertRefused(await reorder, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));
    }

    // US-2 · "A deactivated project keeps its order": the backlog and a reorder answer «Проект не найден.», nothing changes, and
    // the order is back on restore.
    [Fact]
    public async Task Reorder_DeactivatedProject_IsNotFound_AndKeepsItsOrderOnRestore()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));
        var placesBefore = await ProjectBoardsTestData.PlacesAsync(factory, world);

        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);
        var answer = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Bottom(s3));

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(placesBefore, await ProjectBoardsTestData.PlacesAsync(factory, world));

        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Active);
        Assert.Equal(Ids(s3, s1, s2, b4), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // --- Who may reorder, and the order of answers ------------------------------------------------------------------

    // US-2 · "Who may reorder" (BR-PB-5): a viewer's direct reorder answers 403 «Недостаточно прав» and changes nothing; the
    // reorders of a member and of an administrator take effect.
    [Fact]
    public async Task Reorder_ByRole_ViewerIsRefused_MemberAndAdministratorCount()
    {
        var (world, s1, s2, s3, b4) = await ProjectAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);

        var refused = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s3), token: viewer.Token);

        BoardsTestData.AssertRefused(refused, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        Assert.Equal("Недостаточно прав", refused.Detail);
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));

        var byMember = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s3), token: member.Token);
        var byAdministrator = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Bottom(s3));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (byMember.Status, byAdministrator.Status));
        Assert.Equal(Ids(s1, s2, b4, s3), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-2 · "The order of answers on a reorder" (mod-016 api-contract § 0.1): 401, the organization's 404, 403, the project's 404,
    // the level's 404, 400 for the position — each before the next.
    [Fact]
    public async Task Reorder_OrderOfAnswers_FromTheOutsideIn()
    {
        var (world, s1, _, _, _) = await ProjectAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var nothing = $"/api/organizations/{world.OrganizationId}/projects/{Guid.CreateVersion7()}/backlogs/stories/reorder";
        var sideways = new { itemId = s1.Id.ToString(), position = "sideways" };

        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{ProjectBoardsTestData.Backlog(world, "stories")}/reorder", BoardsTestData.Top(s1));
        var outsider = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s1), token: MembershipTestData.Outsider());
        var viewerOnProject = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(s1), token: viewer.Token);
        var viewerOnNothing = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, nothing, BoardsTestData.Top(s1), viewer.Token);
        var memberOnNothing = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, nothing, sideways, OrganizationsTokens.SignedIn());
        var memberOnTasks = await ProjectBoardsTestData.ReorderAsync(factory, world, sideways, "tasks");
        var memberSideways = await ProjectBoardsTestData.ReorderAsync(factory, world, sideways);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        BoardsTestData.AssertRefused(outsider, HttpStatusCode.NotFound, "organizations.organization.not_found");
        Assert.Equal("Организация не найдена.", outsider.Detail);
        BoardsTestData.AssertRefused(viewerOnProject, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        BoardsTestData.AssertRefused(viewerOnNothing, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        BoardsTestData.AssertRefused(memberOnNothing, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal("Проект не найден.", memberOnNothing.Detail);
        BoardsTestData.AssertRefused(memberOnTasks, HttpStatusCode.NotFound, "boards.level.not_found");
        BoardsTestData.AssertRefused(memberSideways, HttpStatusCode.BadRequest, "boards.validation_failed");
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));
    }

    // BR-MX-1: a project of another organization answers like one that names nothing, and the reorder writes nothing there.
    [Fact]
    public async Task Reorder_ProjectOfAnotherOrganization_IsNotFound()
    {
        var (world, s1, _, _, _) = await ProjectAsync();
        var foreign = await BoardsTestData.CreateWorldAsync(factory);
        var address = $"/api/organizations/{world.OrganizationId}/projects/{foreign.ProjectId}/backlogs/stories/reorder";

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, address, BoardsTestData.Top(s1), OrganizationsTokens.SignedIn());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, foreign));
    }
}
