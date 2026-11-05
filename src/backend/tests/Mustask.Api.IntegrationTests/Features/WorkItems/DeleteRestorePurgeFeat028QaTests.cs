using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Process;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 QA pass over the merge of feat-028 (teams of the organization) · the meeting points the developers' suites leave out,
/// written from the two specs before the resolution code was read (qa.md, "QA — merging main (feat-028)"; each test names its
/// plan row): the counts in any state across Delete / Restore / Purge (M4), a team's deletion reaching a deleted item of a
/// deactivated project and touching no other team's item or link (M3), BR-DR-8's order of answers against feat-028's two
/// team refusals (M7), a team's deletion against Delete and Purge, an unlink against a save of a deleted item, a purge of a
/// parent whose children a team owns against that team's unlink and deletion — in both orders and both id orders (M9, M10) —
/// and a storm of item writes and team clean-ups (M11).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteRestorePurgeFeat028QaTests(MustaskApiFactory factory)
{
    private const string Deleted = "workitems.workitem.deleted";

    private sealed record Row(
        Guid? DeletedFromStateId, string? DeletedBy, DateTimeOffset? DeletedAt, Guid StateId, Guid? OwningTeamId, Guid? ParentId, DateTimeOffset ChangedAt)
    {
        public string Marks => $"{DeletedFromStateId}|{DeletedBy}|{DeletedAt:O}";
    }

    // --- Requests and reads -----------------------------------------------------------------------------------------------

    private Task<Answer> PostAsync(WorkItemsWorld world, string identifier, string action, object? body = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world, identifier)}/{action}", body, OrganizationsTokens.SignedIn(), gate);

    private Task<Answer> UnlinkAsync(BoardsWorld world, Guid teamId, Guid? destination, CommitGate? gate = null) =>
        TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, teamId, TeamTestData.To(destination), gate: gate);

    private Task<Answer> DeleteTeamAsync(BoardsWorld world, Guid teamId, CommitGate? gate = null) =>
        TeamTestData.DeleteTeamAsync(factory, world.OrganizationId, teamId, gate: gate);

    private async Task<Row> RowAsync(Item item)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().WorkItems
            .AsNoTracking()
            .Where(stored => stored.Id == item.Id)
            .Select(stored => new Row(
                stored.DeletedFromStateId, stored.DeletedBy, stored.DeletedAt, stored.StateId, stored.OwningTeamId, stored.ParentId, stored.ChangedAt))
            .SingleAsync();
    }

    /// <summary>Whether <paramref name="item"/> comes first among them in the database's own order of ids, the order row locks are taken in.</summary>
    private async Task<bool> IsLowestIdAsync(Item item, IReadOnlyCollection<Item> others)
    {
        var ids = others.Select(other => other.Id).Append(item.Id).ToArray();
        await using var scope = factory.Services.CreateAsyncScope();

        var ordered = await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().WorkItems
            .AsNoTracking()
            .Where(stored => ids.Contains(stored.Id))
            .OrderBy(stored => stored.Id)
            .Select(stored => stored.Id)
            .ToListAsync();

        return ordered[0] == item.Id;
    }

    private async Task<int> UnlinkCountAsync(BoardsWorld world, Guid teamId)
    {
        var answer = await TeamTestData.GetAsync(factory, $"{TeamTestData.ProjectTeam(world.OrganizationId, world.ProjectId, teamId)}/unlink-count");
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.GetProperty("count").GetInt32();
    }

    /// <summary>The deletion's counts by project id: name, active, count.</summary>
    private async Task<Dictionary<Guid, (string Name, bool Active, int Count)>> DeletionCountsAsync(Guid organizationId, Guid teamId)
    {
        var answer = await TeamTestData.GetAsync(factory, $"{TeamTestData.Team(organizationId, teamId)}/deletion-counts");
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.GetProperty("projects").EnumerateArray().ToDictionary(
            project => project.GetProperty("projectId").GetGuid(),
            project => (project.GetProperty("name").GetString()!, project.GetProperty("active").GetBoolean(), project.GetProperty("count").GetInt32()));
    }

    private static bool IsRemoved(WorkItemsWorld world, Item item, Row row) =>
        row.StateId == world.Types[item.Identifier.Split('-')[1]].StateId(StateCategory.Removed);

    private async Task<Item> StoryAsync(BoardsWorld world, string title, Guid? areaId = null) =>
        await BoardsTestData.CreateAsync(factory, world, "US", title, areaId ?? world.PlatformAreaId);

    // --- M4 · the any-state row: counts ----------------------------------------------------------------------------------------

    // The unlink's count and the deletion's counts count a deleted item like a live one (spec § Surfaces and writes, "counted in any
    // state"; feat-028 SC-09, doc-012 BR-MX-12), another project of the team has its own number (a deactivated project is listed,
    // its deleted items counted), a Delete or a Restore moves the number by nothing and a Purge by one, and the number is exactly
    // what the unlink then gives the destination — the deleted items among it, the other project's untouched.
    [Fact]
    public async Task M4_Counts_AreInAnyState_MoveByNothingOnDeleteAndRestore_ByOneOnPurge_AndEqualWhatTheUnlinkReassigns()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var live = await StoryAsync(world, "Живая");
        var willBeDeleted = await StoryAsync(world, "Будет удалена");
        var restoredLater = await StoryAsync(world, "Вернётся");
        var purgedLater = await StoryAsync(world, "Исчезнет");
        var theirs = await BoardsTestData.CreateAsync(factory, world, "US", "Чужая");
        await LinksTestData.DeleteAsync(factory, world.Items, restoredLater);
        await LinksTestData.DeleteAsync(factory, world.Items, purgedLater);

        // A second project of the organization, to which «Платформа» is linked too, with one deleted item it owns there.
        var second = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "SEC", "Второй");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await TeamTestData.LinkTeamsAsync(factory, world.OrganizationId, second.ProjectId, new { teamIds = new[] { world.PlatformId.ToString() } })).Status);
        var created = await WorkItemsTestData.CreateAsync(factory, second, "US", "Во втором");
        var inSecond = new Item(created.Body.GetProperty("id").GetGuid(), created.Body.GetProperty("identifier").GetString()!);
        await OwningTeamTestData.SetOwningTeamAsync(factory, inSecond.Id, world.PlatformId);
        await LinksTestData.DeleteAsync(factory, second, inSecond);

        Assert.Equal(4, await UnlinkCountAsync(world, world.PlatformId));
        var counts = await DeletionCountsAsync(world.OrganizationId, world.PlatformId);
        Assert.Equal([4, 1], [counts[world.ProjectId].Count, counts[second.ProjectId].Count]);

        // Delete of a live owned item: still four. Restore of a deleted one: still four. Purge of a deleted one: three.
        await LinksTestData.DeleteAsync(factory, world.Items, willBeDeleted);
        Assert.Equal(4, await UnlinkCountAsync(world, world.PlatformId));
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(world.Items, restoredLater.Identifier, "restore")).Status);
        Assert.Equal(4, await UnlinkCountAsync(world, world.PlatformId));
        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(world.Items, purgedLater.Identifier, "purge")).Status);
        Assert.Equal(3, await UnlinkCountAsync(world, world.PlatformId));

        // A deactivated project stays in the deletion's counts (BR-OL-9), its deleted item counted.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await OrganizationsApi.SendAsync(
                factory, HttpMethod.Post, $"/api/organizations/{world.OrganizationId}/projects/{second.ProjectId}/deactivate", token: OrganizationsTokens.SignedIn())).Status);
        counts = await DeletionCountsAsync(world.OrganizationId, world.PlatformId);
        Assert.Equal((false, 1, true, 3), (counts[second.ProjectId].Active, counts[second.ProjectId].Count, counts[world.ProjectId].Active, counts[world.ProjectId].Count));

        // The number is what moves: the destination gets the three (the deleted one included) and the one it owned; the other project's
        // item stays «Платформа»'s.
        Assert.Equal(HttpStatusCode.NoContent, (await UnlinkAsync(world, world.PlatformId, world.ProjectTeamId)).Status);
        Assert.Equal(4, await TeamTestData.OwnedItemCountAsync(factory, world.ProjectTeamId));
        Assert.Equal(1, await TeamTestData.OwnedItemCountAsync(factory, world.PlatformId));
        Assert.Equal(world.ProjectTeamId, (await RowAsync(willBeDeleted)).OwningTeamId);
        Assert.Equal(world.PlatformId, (await RowAsync(inSecond)).OwningTeamId);

        // M8 · the destination's backlog lists the live ones, unplaced, and not the deleted one.
        var rows = await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId);
        Assert.Equal(
            new[] { theirs.Identifier, live.Identifier, restoredLater.Identifier }.Order(StringComparer.Ordinal),
            rows.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(willBeDeleted.Identifier, rows);
    }

    // --- M3 · a team's deletion reaches the deleted ones, everywhere, and nothing else ---------------------------------------------

    // BR-DR-9 · a deleted item of a deactivated project that the team owns is released with the rest and stays deleted (marks as they
    // were); a deleted parent of the team is released while its live child of another team keeps its owner and its link (BR-DR-4);
    // after the project is restored, a Restore of the released item brings it back owned by nobody (BR-DR-10).
    [Fact]
    public async Task M3_TeamDeletion_ReleasesDeletedItems_OfADeactivatedProjectToo_AndTouchesNoOtherTeamsItemOrLink()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var second = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "SEC", "Второй");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await TeamTestData.LinkTeamsAsync(factory, world.OrganizationId, second.ProjectId, new { teamIds = new[] { world.PlatformId.ToString() } })).Status);
        var created = await WorkItemsTestData.CreateAsync(factory, second, "US", "Во втором");
        var inSecond = new Item(created.Body.GetProperty("id").GetGuid(), created.Body.GetProperty("identifier").GetString()!);
        await OwningTeamTestData.SetOwningTeamAsync(factory, inSecond.Id, world.PlatformId);
        await LinksTestData.DeleteAsync(factory, second, inSecond);

        var parent = await StoryAsync(world, "Родитель команды");
        var child = await BoardsTestData.CreateAsync(factory, world, "TASK", "Дитя другой команды");
        await OwningTeamTestData.SetOwningTeamAsync(factory, child.Id, world.ProjectTeamId);
        await LinksTestData.LinkAsync(factory, world.Items, child, parent);
        var otherDeleted = await BoardsTestData.CreateAsync(factory, world, "US", "Чужая удалённая");
        await LinksTestData.DeleteAsync(factory, world.Items, parent);
        await LinksTestData.DeleteAsync(factory, world.Items, otherDeleted);

        var parentBefore = await RowAsync(parent);
        var childBefore = await RowAsync(child);
        var otherBefore = await RowAsync(otherDeleted);
        var secondBefore = await RowAsync(inSecond);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await OrganizationsApi.SendAsync(
                factory, HttpMethod.Post, $"/api/organizations/{world.OrganizationId}/projects/{second.ProjectId}/deactivate", token: OrganizationsTokens.SignedIn())).Status);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteTeamAsync(world, world.PlatformId)).Status);

        var parentAfter = await RowAsync(parent);
        var secondAfter = await RowAsync(inSecond);
        Assert.Null(parentAfter.OwningTeamId);
        Assert.Null(secondAfter.OwningTeamId);
        Assert.True(IsRemoved(world.Items, parent, parentAfter) && IsRemoved(second, inSecond, secondAfter));
        Assert.Equal((parentBefore.Marks, secondBefore.Marks), (parentAfter.Marks, secondAfter.Marks));
        Assert.Equal((parentBefore.StateId, secondBefore.StateId), (parentAfter.StateId, secondAfter.StateId));
        Assert.True(parentAfter.ChangedAt >= parentBefore.ChangedAt && secondAfter.ChangedAt >= secondBefore.ChangedAt);

        // Another team's item, deleted or live, with its link to the released parent: not touched, «Изменён» included.
        Assert.Equal(childBefore, await RowAsync(child));
        Assert.Equal(otherBefore, await RowAsync(otherDeleted));
        Assert.Equal(parent.Id, (await RowAsync(child)).ParentId);

        // The project comes back, and so does the item: with nobody as its owner.
        Assert.Equal(
            HttpStatusCode.OK,
            (await OrganizationsApi.SendAsync(
                factory, HttpMethod.Post, $"/api/organizations/{world.OrganizationId}/projects/{second.ProjectId}/restore", token: OrganizationsTokens.SignedIn())).Status);
        var restored = await PostAsync(second, inSecond.Identifier, "restore");
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Null(OwningTeamTestData.TeamOf(restored.Body));
        Assert.Equal(JsonValueKind.Null, restored.Body.GetProperty("deletion").ValueKind);
    }

    // --- M7 · BR-DR-8's order of answers against feat-028's two team refusals ----------------------------------------------------

    // A save that names a team the item cannot have — one linked to no project (409 not_in_project), a deleted one or another
    // organization's (404 team.not_found) — answers 409 workitems.workitem.deleted on a deleted item, the item as it was; the same
    // bodies on a live item get the team's own refusals (so the test would notice an order that put the team first).
    [Fact]
    public async Task M7_Save_OfADeletedItem_NamingAnUnlinkedDeletedOrForeignTeam_Is409Deleted_ALiveItemGetsTheTeamsOwnRefusal()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var live = await StoryAsync(world, "Живая");
        var gone = await StoryAsync(world, "Удалённая");
        await LinksTestData.DeleteAsync(factory, world.Items, gone);
        var before = await RowAsync(gone);

        var unlinked = await TeamTestData.CreateTeamAsync(factory, world.OrganizationId, new { name = "Свободная" });
        var former = await TeamTestData.CreateTeamAsync(factory, world.OrganizationId, new { name = "Бывшая" });
        Assert.Equal(HttpStatusCode.NoContent, (await TeamTestData.DeleteTeamAsync(factory, world.OrganizationId, former.Body.GetProperty("id").GetGuid())).Status);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var foreign = await TeamTestData.CreateTeamAsync(factory, other.Id, new { name = "Чужая" });
        Assert.Equal(HttpStatusCode.Created, unlinked.Status);
        Assert.Equal(HttpStatusCode.Created, foreign.Status);

        var cases = new (string Team, string LiveCode, HttpStatusCode LiveStatus)[]
        {
            (unlinked.Body.GetProperty("id").GetString()!, "organizations.team.not_in_project", HttpStatusCode.Conflict),
            (former.Body.GetProperty("id").GetString()!, "organizations.team.not_found", HttpStatusCode.NotFound),
            (foreign.Body.GetProperty("id").GetString()!, "organizations.team.not_found", HttpStatusCode.NotFound),
        };

        foreach (var (team, liveCode, liveStatus) in cases)
        {
            var body = new { team = new { teamId = team } };

            LinksTestData.AssertRefused(await WorkItemsTestData.SaveAsync(factory, world.Items, live.Identifier, body), liveStatus, liveCode);
            LinksTestData.AssertRefused(await WorkItemsTestData.SaveAsync(factory, world.Items, gone.Identifier, body), HttpStatusCode.Conflict, Deleted);
        }

        Assert.Equal(before, await RowAsync(gone));
    }

    // --- M9 · a team's deletion against Delete and Purge; an unlink against a save of a deleted item -------------------------------

    // The deletion first: the Delete waits on the item's row, then deletes an item nobody owns (the held state is the Delete's).
    [Fact]
    public async Task M9_TeamDeletion_Versus_Delete_DeletionFirst_TheDeleteWaits_ThenDeletesAnItemNobodyOwns()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await StoryAsync(world, "История");
        var neighbour = await StoryAsync(world, "Соседка");
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", "Активна")).Status);

        var (teamDeleted, deleted) = await TeamTestData.RaceAsync(
            factory, gate => DeleteTeamAsync(world, world.PlatformId, gate), _ => PostAsync(world.Items, story.Identifier, "delete"));

        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Null(OwningTeamTestData.TeamOf(deleted.Body));
        Assert.Equal("Активна", deleted.Body.GetProperty("deletion").GetProperty("fromState").GetProperty("name").GetString());
        Assert.Null((await RowAsync(neighbour)).OwningTeamId);
        Assert.Equal(0, await OwningTeamTestData.PlacesNamingAnotherTeamAsync(factory, world.ProjectId));
    }

    // The Delete first: the deletion waits, then releases the item that is deleted now; its marks and state are the Delete's.
    [Fact]
    public async Task M9_TeamDeletion_Versus_Delete_DeleteFirst_TheDeletionWaits_ThenReleasesTheDeletedItem_MarksUntouched()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await StoryAsync(world, "История");
        var neighbour = await StoryAsync(world, "Соседка");

        var (deleted, teamDeleted) = await TeamTestData.RaceAsync(
            factory, gate => PostAsync(world.Items, story.Identifier, "delete", gate: gate), _ => DeleteTeamAsync(world, world.PlatformId));

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        var read = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(deleted.Body.GetProperty("deletion").GetRawText(), read.GetProperty("deletion").GetRawText());
        Assert.Equal("Removed", read.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
        Assert.Null(OwningTeamTestData.TeamOf(read));
        Assert.Null((await RowAsync(neighbour)).OwningTeamId);
        Assert.Equal(0, await OwningTeamTestData.PlacesNamingAnotherTeamAsync(factory, world.ProjectId));
    }

    // The deletion first: the purge waits on the row, then removes the released item.
    [Fact]
    public async Task M9_TeamDeletion_Versus_Purge_DeletionFirst_ThePurgeWaits_ThenRemovesTheReleasedItem()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await StoryAsync(world, "История");
        var neighbour = await StoryAsync(world, "Соседка");
        await LinksTestData.DeleteAsync(factory, world.Items, story);

        var (teamDeleted, purged) = await TeamTestData.RaceAsync(
            factory, gate => DeleteTeamAsync(world, world.PlatformId, gate), _ => PostAsync(world.Items, story.Identifier, "purge"));

        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, story.Identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        Assert.Null((await RowAsync(neighbour)).OwningTeamId);
    }

    // The purge first: the deletion waits, then finds the item gone and releases what is left.
    [Fact]
    public async Task M9_TeamDeletion_Versus_Purge_PurgeFirst_TheDeletionWaits_ThenReleasesWhatIsLeft()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await StoryAsync(world, "История");
        var neighbour = await StoryAsync(world, "Соседка");
        await LinksTestData.DeleteAsync(factory, world.Items, story);

        var (purged, teamDeleted) = await TeamTestData.RaceAsync(
            factory, gate => PostAsync(world.Items, story.Identifier, "purge", gate: gate), _ => DeleteTeamAsync(world, world.PlatformId));

        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, story.Identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        Assert.Null((await RowAsync(neighbour)).OwningTeamId);
    }

    // The unlink first, holding the link: a save of a deleted item that names the unlinked team queues on the link's lock, and
    // answers 409 deleted when it runs — not the team's 409 not_in_project that the unlink would otherwise earn it.
    [Fact]
    public async Task M9_Unlink_Versus_Save_OfADeletedItem_NamingTheUnlinkedTeam_TheSaveWaitsOnTheLink_ThenAnswers409Deleted()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await StoryAsync(world, "История");
        await LinksTestData.DeleteAsync(factory, world.Items, story);

        var (unlinked, saved) = await TeamTestData.RaceAsync(
            factory,
            gate => UnlinkAsync(world, world.PlatformId, world.ProjectTeamId, gate),
            _ => WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { team = new { teamId = world.PlatformId.ToString() } }));

        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        LinksTestData.AssertRefused(saved, HttpStatusCode.Conflict, Deleted);
        Assert.Equal(world.ProjectTeamId, (await RowAsync(story)).OwningTeamId);
    }

    // --- M10 · a purge that rewrites children against the team's clean-up -------------------------------------------------------

    // A deleted parent with three tasks the team owns (one of them deleted too), purged against the team's unlink or deletion,
    // whichever starts first, with the parent's id below the children's and above them (a row order is what a deadlock needs):
    // both end 204, the parent is gone, each child lost its parent, kept its state and marks, and went to the destination or to
    // nobody; the deleted child is still deleted.
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public async Task M10_Purge_OfADeletedParent_WithChildrenTheTeamOwns_Versus_ItsCleanUp_EndsInOneOrder_NoDeadlock(
        bool parentCreatedFirst, bool cleanUpFirst, bool teamDeletion)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var children = new List<Item>();
        Item parent;

        if (parentCreatedFirst)
        {
            parent = await StoryAsync(world, "Родитель");
        }
        else
        {
            parent = new Item(Guid.Empty, string.Empty);
        }

        for (var index = 0; index < 3; index++)
        {
            children.Add(await BoardsTestData.CreateAsync(factory, world, "TASK", $"Задача {index}", world.PlatformAreaId));
        }

        if (!parentCreatedFirst)
        {
            parent = await StoryAsync(world, "Родитель");
        }

        foreach (var child in children)
        {
            await LinksTestData.LinkAsync(factory, world.Items, child, parent);
        }

        await LinksTestData.DeleteAsync(factory, world.Items, parent);
        await LinksTestData.DeleteAsync(factory, world.Items, children[2]);
        Assert.Equal(parentCreatedFirst, await IsLowestIdAsync(parent, children));
        var deletedChildBefore = await RowAsync(children[2]);

        Func<CommitGate?, Task<Answer>> cleanUp = gate => teamDeletion
            ? DeleteTeamAsync(world, world.PlatformId, gate)
            : UnlinkAsync(world, world.PlatformId, world.ProjectTeamId, gate);
        Func<CommitGate?, Task<Answer>> purge = gate => PostAsync(world.Items, parent.Identifier, "purge", gate: gate);

        var (first, second) = cleanUpFirst
            ? await TeamTestData.RaceAsync(factory, cleanUp, purge)
            : await TeamTestData.RaceAsync(factory, purge, cleanUp);

        Assert.Equal(HttpStatusCode.NoContent, first.Status);
        Assert.Equal(HttpStatusCode.NoContent, second.Status);
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, parent.Identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");

        Guid? expectedOwner = teamDeletion ? null : world.ProjectTeamId;

        foreach (var child in children)
        {
            var row = await RowAsync(child);
            Assert.Null(row.ParentId);
            Assert.Equal(expectedOwner, row.OwningTeamId);
        }

        var deletedChild = await RowAsync(children[2]);
        Assert.Equal((deletedChildBefore.StateId, deletedChildBefore.Marks), (deletedChild.StateId, deletedChild.Marks));
        Assert.Equal(0, await OwningTeamTestData.PlacesNamingAnotherTeamAsync(factory, world.ProjectId));
    }

    // --- M11 · a storm of item writes and team clean-ups ------------------------------------------------------------------------

    // Three rounds of fifty item writes (Delete, Restore held and chosen, Save of a title, a state and a team, Purge) on nine
    // stories of three teams, one clean-up per round fired among them — an unlink to a team, a team's deletion, an unlink to nobody:
    // every answer is a success or a refusal the contracts name, no server error and no deadlock, every clean-up answers 204, and
    // what is stored agrees — no item owned by a team that is not linked to its project, no team place naming another team, a live
    // item carries no mark and a deleted one all three.
    [Fact]
    public async Task M11_Storm_OfItemWritesAndTeamCleanUps_NoServerError_NoItemOwnedByAnUnlinkedTeam_MarksAgree()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var (analyticsId, _) = await BoardsTestData.AddTeamAsync(factory, world, "Аналитика");
        var teams = new[] { world.PlatformId, world.ProjectTeamId, analyticsId };
        var stories = new List<Item>();

        for (var index = 0; index < 9; index++)
        {
            var created = await WorkItemsTestData.CreateAsync(factory, world.Items, "US", $"История {index}");
            var item = new Item(created.Body.GetProperty("id").GetGuid(), created.Body.GetProperty("identifier").GetString()!);
            await OwningTeamTestData.SetOwningTeamAsync(factory, item.Id, teams[index % 3]);
            stories.Add(item);
        }

        var states = world.Items.Types["US"];
        var random = new Random(20261019);
        var allowed = new HashSet<string?>
        {
            Deleted, "workitems.workitem.not_deleted", "workitems.workitem.state_not_allowed", "workitems.workitem.not_found",
            "organizations.team.not_found", "organizations.team.not_in_project",
        };
        var cleanUps = new Func<Task<Answer>>[]
        {
            () => UnlinkAsync(world, world.PlatformId, world.ProjectTeamId),
            () => DeleteTeamAsync(world, analyticsId),
            () => UnlinkAsync(world, world.ProjectTeamId, null),
        };
        var violations = new List<string>();

        for (var round = 0; round < cleanUps.Length; round++)
        {
            var requests = Enumerable.Range(0, 50).Select(_ =>
            {
                var item = stories[random.Next(stories.Count)];
                var kind = random.Next(100);

                return kind switch
                {
                    < 20 => PostAsync(world.Items, item.Identifier, "delete"),
                    < 40 => PostAsync(world.Items, item.Identifier, "restore"),
                    < 50 => PostAsync(world.Items, item.Identifier, "restore", new { stateId = states.StateId("Активна").ToString() }),
                    < 62 => WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { title = $"Новое {random.Next(1000)}" }),
                    < 72 => WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { stateId = states.StateId("Активна").ToString() }),
                    < 90 => WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { team = new { teamId = teams[random.Next(teams.Length)].ToString() } }),
                    _ => PostAsync(world.Items, item.Identifier, "purge"),
                };
            }).ToList();
            var cleanUp = cleanUps[round]();
            requests.Insert(random.Next(requests.Count + 1), cleanUp);

            foreach (var answer in await Task.WhenAll(requests))
            {
                var ok = (int)answer.Status is >= 200 and < 300;

                if (!ok && !(answer.Status is HttpStatusCode.Conflict or HttpStatusCode.NotFound && allowed.Contains(answer.ErrorCode)))
                {
                    violations.Add($"round {round}: {(int)answer.Status} {answer.ErrorCode}");
                }
            }

            Assert.Equal(HttpStatusCode.NoContent, (await cleanUp).Status);
        }

        Assert.True(violations.Count == 0, string.Join("; ", violations.Distinct()));
        Assert.Equal(0, await OwningTeamTestData.PlacesNamingAnotherTeamAsync(factory, world.ProjectId));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var linked = await db.ProjectTeams.AsNoTracking().Where(link => link.ProjectId == world.ProjectId).Select(link => link.TeamId).ToListAsync();
        var stored = await (
                from item in db.WorkItems.AsNoTracking()
                join state in db.StateDefinitions on item.StateId equals state.Id
                where item.ProjectId == world.ProjectId
                select new { item.Number, item.OwningTeamId, state.Category, item.DeletedFromStateId, item.DeletedBy, item.DeletedAt })
            .ToListAsync();

        foreach (var row in stored)
        {
            Assert.True(row.OwningTeamId is null || linked.Contains(row.OwningTeamId.Value), $"Item {row.Number} is owned by a team that is not linked.");

            var marks = new[] { row.DeletedFromStateId is not null, row.DeletedBy is not null, row.DeletedAt is not null };

            Assert.True(
                row.Category == StateCategory.Removed ? marks.All(mark => mark) : marks.All(mark => !mark),
                $"Item {row.Number}: marks disagree with the state's category {row.Category}.");
        }
    }
}
