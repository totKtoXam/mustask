using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 US-2 · the surface matrix (ADR-0030 rule 2, spec § "Surfaces and writes"): one world with a deleted task <b>D</b>
/// beneath a live story <b>S</b>, a live child <b>C</b> and a deleted child <b>DC</b> of D, and a live task <b>L</b> beneath S, and
/// every surface that exists read once, each asserted present or absent exactly as the spec's table says — one block per row of
/// the table. This is what makes a forgotten surface fail: a later line that adds a surface (or reads work items another way)
/// adds a row here, and a surface with no row is what review looks for. The parity of the three definitions of "deleted" is
/// <c>DeletedStateParityTests</c>'.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeletedItemSurfaceMatrixTests(MustaskApiFactory factory)
{
    private sealed record Matrix(BoardsWorld World, Item S, Item D, Item C, Item DC, Item L, Item FD, Item SC);

    /// <summary>
    /// S (story) with L and D (tasks) beneath it, D with C and DC beneath it; D and DC deleted. A deleted feature FD with a live
    /// story SC beneath it, for the tree's top-level rows. All in «Платформа».
    /// </summary>
    private async Task<Matrix> SeedAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var s = await BoardsTestData.CreateAsync(factory, world, "US", "История S", world.PlatformAreaId);
        var l = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача L", world.PlatformAreaId);
        var d = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача D", world.PlatformAreaId);
        var c = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача C", world.PlatformAreaId);
        var dc = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача DC", world.PlatformAreaId);
        var fd = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича FD", world.PlatformAreaId);
        var sc = await BoardsTestData.CreateAsync(factory, world, "US", "История SC", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, sc, fd);
        await LinksTestData.LinkAsync(factory, world.Items, l, s);
        await LinksTestData.LinkAsync(factory, world.Items, d, s);
        await LinksTestData.LinkAsync(factory, world.Items, c, d);
        await LinksTestData.LinkAsync(factory, world.Items, dc, d);
        await LinksTestData.DeleteAsync(factory, world.Items, dc);
        await LinksTestData.DeleteAsync(factory, world.Items, d);
        await LinksTestData.DeleteAsync(factory, world.Items, fd);

        return new Matrix(world, s, d, c, dc, l, fd, sc);
    }

    private static string[] Identifiers(IEnumerable<JsonElement> rows) =>
        rows.Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    private static string[] AllIdentifiers(IEnumerable<JsonElement> tree) =>
        BacklogTreeTestData.AllRows(tree).Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    [Fact]
    public async Task EverySurfaceOfTheSpecsTable_ShowsOrHidesTheDeletedItems_AsItSays()
    {
        var m = await SeedAsync();
        var world = m.World;

        // Row 1 · the project's list and its total: not shown, not counted.
        var list = await WorkItemsTestData.ListAsync(factory, world.Items);
        var listed = list.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();
        Assert.Equal(new[] { m.S, m.L, m.C, m.SC }.Select(item => item.Identifier).Order(StringComparer.Ordinal), listed.Order(StringComparer.Ordinal));
        Assert.Equal(4, list.Body.GetProperty("count").GetInt32());

        // Row 2 · the card by address: read in full, with its deletion, whoever asks to read.
        foreach (var gone in new[] { m.D, m.DC, m.FD })
        {
            var card = await WorkItemsTestData.ReadAsync(factory, world.Items, gone.Identifier);
            Assert.Equal("Removed", card.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
            Assert.Equal(JsonValueKind.Object, card.GetProperty("deletion").ValueKind);
        }

        // Row 3 · the team's backlog and board of the story level, and the project's: the story and its live task stay, no row, card
        // or column holds a deleted item.
        var teamBacklog = await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId);
        var teamTree = teamBacklog.Body.GetProperty("items").EnumerateArray().ToArray();
        var teamRows = AllIdentifiers(teamTree);
        Assert.DoesNotContain(m.D.Identifier, teamRows);
        Assert.DoesNotContain(m.DC.Identifier, teamRows);
        Assert.Contains(m.S.Identifier, Identifiers(teamTree));

        var projectBacklog = await ProjectBoardsTestData.GetBacklogAsync(factory, world);
        var projectTree = projectBacklog.Body.GetProperty("items").EnumerateArray().ToArray();
        Assert.DoesNotContain(m.D.Identifier, AllIdentifiers(projectTree));
        Assert.DoesNotContain(m.DC.Identifier, AllIdentifiers(projectTree));

        var teamBoard = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId);
        var projectBoard = await ProjectBoardsTestData.GetBoardAsync(factory, world);
        Assert.DoesNotContain(m.D.Identifier, CardsAndChecklists(teamBoard.Body));
        Assert.DoesNotContain(m.DC.Identifier, CardsAndChecklists(teamBoard.Body));
        Assert.DoesNotContain(m.D.Identifier, CardsAndChecklists(projectBoard.Body));
        Assert.DoesNotContain(m.DC.Identifier, CardsAndChecklists(projectBoard.Body));

        // Row 4 · the backlog tree: a deleted parent stands in no row and its children are top-level rows, their «Родитель» cell
        // «Нет» — SC, a story whose feature FD is deleted, is a top-level row of the stories backlog; D stands in no row and L
        // nests beneath S as before, D's live child C hanging from no row at all.
        Assert.Equal([m.L.Identifier], AllIdentifiers(BacklogTreeTestData.Row(teamTree, m.S.Identifier).GetProperty("children").EnumerateArray()));
        var scRow = teamTree.Single(row => row.GetProperty("identifier").GetString() == m.SC.Identifier);
        Assert.Null(BacklogTreeTestData.ParentOf(scRow));
        Assert.DoesNotContain(m.FD.Identifier, teamRows);
        Assert.DoesNotContain(m.C.Identifier, AllIdentifiers(BacklogTreeTestData.Row(teamTree, m.S.Identifier).GetProperty("children").EnumerateArray()));

        // Row 5 · «Родитель», the picker, «Дочерние элементы»: C leads to D as «Удалена»; the picker never offers D or DC; S lists
        // L alone; D lists C alone.
        var cCard = await WorkItemsTestData.ReadAsync(factory, world.Items, m.C.Identifier);
        Assert.Equal((m.D.Identifier, "Removed"), (
            LinksTestData.IdentifierOf(cCard.GetProperty("parent")),
            cCard.GetProperty("parent").GetProperty("state").GetProperty("category").GetProperty("code").GetString()));
        foreach (var text in new string?[] { null, "Задача" })
        {
            var offered = await LinksTestData.OfferedAsync(factory, world.Items, m.L.Identifier, text);
            Assert.DoesNotContain(m.D.Identifier, offered);
            Assert.DoesNotContain(m.DC.Identifier, offered);
        }

        Assert.Equal([m.L.Identifier], LinksTestData.Identifiers((await WorkItemsTestData.ReadAsync(factory, world.Items, m.S.Identifier)).GetProperty("children")));
        Assert.Equal([m.C.Identifier], LinksTestData.Identifiers((await WorkItemsTestData.ReadAsync(factory, world.Items, m.D.Identifier)).GetProperty("children")));

        // Row 6 · a board card's checklist and its counter: S's checklist lists L alone.
        Assert.Equal([m.L.Identifier], ChecklistOf(teamBoard.Body, m.S));

        // Row 7 · a team's owned-items count (every state), in the two forms feat-028 gave it — the unlink's count in this project
        // and the deletion's counts over the team's projects (api-contract § 1.5, SC-09) — and an area's deletion guard.
        var count = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{TeamTestData.ProjectTeam(world.OrganizationId, world.ProjectId, world.PlatformId)}/unlink-count",
            token: OrganizationsTokens.SignedIn());
        Assert.Equal(7, count.Body.GetProperty("count").GetInt32());
        var deletionCounts = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{TeamTestData.Team(world.OrganizationId, world.PlatformId)}/deletion-counts",
            token: OrganizationsTokens.SignedIn());
        Assert.Equal([7], deletionCounts.Body.GetProperty("projects").EnumerateArray().Select(project => project.GetProperty("count").GetInt32()));
        var area = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Areas(world.OrganizationId, world.ProjectId)}/{world.PlatformAreaId}",
            token: OrganizationsTokens.SignedIn());
        LinksTestData.AssertRefused(area, HttpStatusCode.Conflict, "organizations.area.not_empty");

        // Row 8 · a deactivated project: its items, deleted ones too, are unreachable; restored, they are as they were.
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, m.D.Identifier);
        var project = OrganizationsApi.Project(world.OrganizationId, world.ProjectId);
        Assert.Equal(HttpStatusCode.NoContent, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/deactivate", token: OrganizationsTokens.SignedIn())).Status);
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, m.D.Identifier), HttpStatusCode.NotFound, "organizations.project.not_found");
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, m.S.Identifier), HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal(HttpStatusCode.OK, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/restore", token: OrganizationsTokens.SignedIn())).Status);
        Assert.Equal(before.GetRawText(), (await WorkItemsTestData.ReadAsync(factory, world.Items, m.D.Identifier)).GetRawText());
    }

    /// <summary>The identifiers of every card of a board and of every checklist row of its cards.</summary>
    private static string[] CardsAndChecklists(JsonElement board) =>
        board.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .SelectMany(card => new[] { card.GetProperty("identifier").GetString()! }
                .Concat(card.TryGetProperty("checklist", out var checklist)
                    ? checklist.EnumerateArray().Select(child => child.GetProperty("identifier").GetString()!)
                    : []))
            .ToArray();

    private static string[] ChecklistOf(JsonElement board, Item card) =>
        board.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Single(candidate => candidate.GetProperty("identifier").GetString() == card.Identifier)
            .GetProperty("checklist").EnumerateArray().Select(child => child.GetProperty("identifier").GetString()!).ToArray();

    // The writes of the spec's second table, once each on the same world, so the matrix of surfaces and the matrix of writes are
    // read together: a form save and a card move and a tick are refused 409 deleted; the parent routes 409 item_deleted; a
    // reorder naming the item is stale; a second delete is refused; a live child may release its link to a deleted parent.
    [Fact]
    public async Task EveryWriteOfTheSpecsTable_IsRefusedOrAllowed_AsItSays()
    {
        var m = await SeedAsync();
        var world = m.World;
        var dBefore = await LinksTestData.StoredAsync(factory, m.D);

        // Row 1 and 2 · a form save, a card move and a tick are one save: 409 deleted.
        LinksTestData.AssertRefused(
            await WorkItemsTestData.SaveAsync(factory, world.Items, m.D.Identifier, new { title = "Другое" }), HttpStatusCode.Conflict, "workitems.workitem.deleted");
        LinksTestData.AssertRefused(await BoardsTestData.SetStateAsync(factory, world, m.D, "TASK", "Закрыта"), HttpStatusCode.Conflict, "workitems.workitem.deleted");

        // Row 3 · the parent route: the item as child or as new parent; a creation beneath it; a live item's save naming it.
        LinksTestData.AssertRefused(
            await LinksTestData.SetParentAsync(factory, world.Items, m.D.Identifier, m.S.Id, m.S.Id), HttpStatusCode.Conflict, "workitems.link.item_deleted");
        LinksTestData.AssertRefused(
            await LinksTestData.SetParentAsync(factory, world.Items, m.L.Identifier, m.D.Id, m.S.Id), HttpStatusCode.Conflict, "workitems.link.item_deleted");
        LinksTestData.AssertRefused(
            await BacklogTreeTestData.CreateChildAsync(factory, world, "TASK", "Под удалённой", m.D.Id.ToString()), HttpStatusCode.Conflict, "workitems.link.item_deleted");
        LinksTestData.AssertRefused(
            await WorkItemsTestData.SaveAsync(factory, world.Items, m.L.Identifier, new { parent = new { parentId = m.D.Id.ToString(), seenParentId = m.S.Id.ToString() } }),
            HttpStatusCode.Conflict,
            "workitems.link.item_deleted");

        // Row 4 · a reorder naming it as moved or anchor.
        LinksTestData.AssertRefused(await BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Top(m.D)), HttpStatusCode.Conflict, "boards.backlog.reorder_stale");

        // Row 5 · a second Delete.
        LinksTestData.AssertRefused(
            await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, m.D.Identifier)}/delete", token: OrganizationsTokens.SignedIn()),
            HttpStatusCode.Conflict,
            "workitems.workitem.deleted");

        Assert.Equal(dBefore, await LinksTestData.StoredAsync(factory, m.D));

        // Row 6 · a live child's release of its link to a deleted parent is allowed; the deleted parent, its «Изменён» too, is unchanged.
        var release = await WorkItemsTestData.SaveAsync(
            factory, world.Items, m.C.Identifier, new { parent = new { parentId = (string?)null, seenParentId = m.D.Id.ToString() } });
        Assert.Equal(HttpStatusCode.OK, release.Status);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, m.C));
        Assert.Equal(dBefore, await LinksTestData.StoredAsync(factory, m.D));

        // Row 7 · Restore, Purge and a team's deletion releasing its owner are allowed.
        Assert.Equal(
            HttpStatusCode.OK,
            (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, m.D.Identifier)}/restore", token: OrganizationsTokens.SignedIn())).Status);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, m.DC.Identifier)}/purge", token: OrganizationsTokens.SignedIn())).Status);
        // feat-028: an unlink moves the items the team owns in the project to its destination, and a team's deletion releases what
        // it owns to none — both in any state (BR-DR-9), the still-deleted FD included; here the unlink hands «Платформа»'s items to
        // «Мастаск» and the deletion of «Мастаск» then releases them. Each one's own test is DeletedItemViewsEndpointTests'.
        var unlink = await TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.PlatformId, TeamTestData.To(world.ProjectTeamId));
        Assert.Equal(HttpStatusCode.NoContent, unlink.Status);
        var teamDeletion = await TeamTestData.DeleteTeamAsync(factory, world.OrganizationId, world.ProjectTeamId);
        Assert.Equal(HttpStatusCode.NoContent, teamDeletion.Status);
        var fd = await WorkItemsTestData.ReadAsync(factory, world.Items, m.FD.Identifier);
        Assert.Equal((JsonValueKind.Null, "Removed"), (fd.GetProperty("team").ValueKind, fd.GetProperty("state").GetProperty("category").GetProperty("code").GetString()));
    }

    // --- The reads of the organization-addressed set, classified ------------------------------------------------------------

    private enum Reads
    {
        /// <summary>Reads no work item: a container, a team, its people, the process, a list of candidates.</summary>
        NoWorkItem,

        /// <summary>Reads the items outside «Удалена» only (the list, the pickers, the backlogs and boards): the rows above.</summary>
        LiveOnly,

        /// <summary>Reads an item whatever its state (the card by address).</summary>
        AnyState,

        /// <summary>Counts the items a team owns, in any state: row 7.</summary>
        CountsOwnedInAnyState,
    }

    private const string Org = "/api/organizations/{organizationId}";
    private const string Project = Org + "/projects/{projectId}";
    private const string OrgTeam = Org + "/teams/{teamId}";
    private const string ProjectTeam = Project + "/teams/{teamId}";

    private static readonly IReadOnlyDictionary<string, Reads> ReadRoutes = new Dictionary<string, Reads>(StringComparer.Ordinal)
    {
        // The reads that touch work items: each is asserted in the block of the same name above.
        ["GET " + Project + "/workitems"] = Reads.LiveOnly,
        ["GET " + Project + "/workitems/{identifier}"] = Reads.AnyState,
        ["GET " + Project + "/workitems/{identifier}/parent-candidates"] = Reads.LiveOnly,
        ["GET " + ProjectTeam + "/backlogs/{level}"] = Reads.LiveOnly,
        ["GET " + ProjectTeam + "/boards/{level}"] = Reads.LiveOnly,
        ["GET " + Project + "/backlogs/{level}"] = Reads.LiveOnly,
        ["GET " + Project + "/boards/{level}"] = Reads.LiveOnly,
        ["GET " + ProjectTeam + "/unlink-count"] = Reads.CountsOwnedInAnyState,
        ["GET " + OrgTeam + "/deletion-counts"] = Reads.CountsOwnedInAnyState,

        // The reads that touch none (feat-028 added the organization's teams, a team's page, its projects, its people and the
        // project's linked teams; none of them counts or lists an item).
        ["GET " + Org] = Reads.NoWorkItem,
        ["GET " + Org + "/access"] = Reads.NoWorkItem,
        ["GET " + Org + "/process"] = Reads.NoWorkItem,
        ["GET " + Org + "/projects"] = Reads.NoWorkItem,
        ["GET " + Org + "/projects/deactivated"] = Reads.NoWorkItem,
        ["GET " + Org + "/members"] = Reads.NoWorkItem,
        ["GET " + Org + "/member-candidates"] = Reads.NoWorkItem,
        ["GET " + Org + "/teams"] = Reads.NoWorkItem,
        ["GET " + Org + "/teams/member-candidates"] = Reads.NoWorkItem,
        ["GET " + Org + "/workitems/assignee-candidates"] = Reads.NoWorkItem,
        ["GET " + OrgTeam] = Reads.NoWorkItem,
        ["GET " + OrgTeam + "/projects"] = Reads.NoWorkItem,
        ["GET " + OrgTeam + "/members"] = Reads.NoWorkItem,
        ["GET " + OrgTeam + "/member-candidates"] = Reads.NoWorkItem,
        ["GET " + Project] = Reads.NoWorkItem,
        ["GET " + Project + "/teams"] = Reads.NoWorkItem,
        ["GET " + Project + "/areas"] = Reads.NoWorkItem,
    };

    // ADR-0030 rule 2 · a read of the organization-addressed set says whether it reads work items, and in which states: a GET route
    // with no row fails here, as a write route with no row fails WorkItemWriteInventoryTests, so the line that adds the next read
    // has to look at this matrix (and add its block above when it does read an item).
    [Fact]
    public void EveryReadRoute_SaysWhetherItReadsWorkItems_AndInWhichStates()
    {
        var reads = OrganizationAddressedRoutes.All.Where(route => !route.IsWrite).Select(route => route.Key).Order(StringComparer.Ordinal);

        Assert.Equal(reads, ReadRoutes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["GET " + Project + "/workitems/{identifier}"],
            ReadRoutes.Where(read => read.Value == Reads.AnyState).Select(read => read.Key));
        Assert.Equal(
            ["GET " + ProjectTeam + "/unlink-count", "GET " + OrgTeam + "/deletion-counts"],
            ReadRoutes.Where(read => read.Value == Reads.CountsOwnedInAnyState).Select(read => read.Key).Order(StringComparer.Ordinal));
    }
}
