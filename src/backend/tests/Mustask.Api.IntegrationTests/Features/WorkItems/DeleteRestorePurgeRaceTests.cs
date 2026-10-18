using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Npgsql;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 BR-DR-11 (ADR-0030 rule 6, plan-backend § 8): Delete, Restore, Purge and the writes they meet end in one order and
/// no server error, in both orders where both orders are possible. Each test holds the first request at its commit gate with
/// every lock it took, sends the second, waits until the second blocks on a lock of the first (or finishes without blocking),
/// releases the first, and asserts both outcomes and the stored state: the second request decides on what it finds under its
/// own lock, and no half state is left.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteRestorePurgeRaceTests(MustaskApiFactory factory)
{
    private const string Deleted = "workitems.workitem.deleted";
    private const string NotDeleted = "workitems.workitem.not_deleted";

    // --- Requests ---------------------------------------------------------------------------------------------------

    private Task<Answer> DeleteAsync(WorkItemsWorld world, Item item, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world, item.Identifier)}/delete", null, token ?? OrganizationsTokens.SignedIn(), gate);

    private Task<Answer> RestoreAsync(WorkItemsWorld world, Item item, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world, item.Identifier)}/restore", null, OrganizationsTokens.SignedIn(), gate);

    private Task<Answer> PurgeAsync(WorkItemsWorld world, Item item, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world, item.Identifier)}/purge", null, OrganizationsTokens.SignedIn(), gate);

    // feat-028: a team belongs to the organization, and its deletion is at the organization's address.
    private Task<Answer> DeleteTeamAsync(BoardsWorld world, Guid teamId, CommitGate? gate = null) =>
        TeamTestData.DeleteTeamAsync(factory, world.OrganizationId, teamId, gate: gate);

    // feat-028: an unlink takes a team off one project and names where its items go (a team, or «Без команды»).
    private Task<Answer> UnlinkAsync(BoardsWorld world, Guid teamId, Guid? destination, CommitGate? gate = null) =>
        TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, teamId, TeamTestData.To(destination), gate: gate);

    private Task<Answer> ReleaseLinkAsync(WorkItemsWorld world, Item child, Item seenParent, CommitGate? gate = null) =>
        WorkItemsTestData.SaveAsync(
            factory, world, child.Identifier, new { parent = new { parentId = (string?)null, seenParentId = seenParent.Id.ToString() } }, gate);

    /// <summary>
    /// Holds <paramref name="first"/> at its commit gate with every lock it took, sends <paramref name="second"/>, waits until the
    /// second blocks on a lock or finishes without blocking, releases the gate, and answers both. <paramref name="secondWaits"/>
    /// says whether the second is expected to be blocked by the first (it is asserted).
    /// </summary>
    private async Task<(Answer First, Answer Second)> RaceAsync(Func<CommitGate, Task<Answer>> first, Func<Task<Answer>> second, bool secondWaits = true)
    {
        using var gate = factory.CommitGates.Open();

        var firstTask = first(gate);
        await Task.WhenAny(gate.Reached, firstTask);
        Assert.False(firstTask.IsCompleted, "The first request finished before reaching its commit gate.");

        var secondTask = second();
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, secondTask);

        if (secondWaits)
        {
            Assert.False(secondTask.IsCompleted, "The second request did not wait for the first's locks.");
        }

        gate.Release();

        return (await firstTask, await secondTask);
    }

    private static string CategoryOf(JsonElement item) => item.GetProperty("state").GetProperty("category").GetProperty("code").GetString()!;

    private async Task<(BoardsWorld World, Item Story)> StoryAsync(bool deleted, string state = "Новая")
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);

        if (state != "Новая")
        {
            Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", state)).Status);
        }

        if (deleted)
        {
            await LinksTestData.DeleteAsync(factory, world.Items, story);
        }

        return (world, story);
    }

    // --- Delete ∥ Delete --------------------------------------------------------------------------------------------------

    // ADR-0030 rule 6 · the second answers 409 deleted; the held state and the deleter are the first's.
    [Fact]
    public async Task Delete_Versus_Delete()
    {
        var (world, story) = await StoryAsync(deleted: false, state: "Активна");
        var first = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        var second = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);

        var (a, b) = await RaceAsync(gate => DeleteAsync(world.Items, story, first.Token, gate), () => DeleteAsync(world.Items, story, second.Token));

        Assert.Equal(HttpStatusCode.OK, a.Status);
        LinksTestData.AssertRefused(b, HttpStatusCode.Conflict, Deleted);
        var deletion = (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("deletion");
        Assert.Equal((first.Id, "Активна"), (deletion.GetProperty("by").GetProperty("id").GetString(), deletion.GetProperty("fromState").GetProperty("name").GetString()));
    }

    // --- Delete ∥ Save, both orders ---------------------------------------------------------------------------------------

    // The save first: the delete records the state the save left.
    [Fact]
    public async Task Delete_Versus_Save_SaveFirst_TheDeleteRecordsTheStateTheSaveLeft()
    {
        var (world, story) = await StoryAsync(deleted: false);

        var (saved, deleted) = await RaceAsync(
            gate => WorkItemsTestData.SaveAsync(
                factory, world.Items, story.Identifier, new { stateId = world.Items.Types["US"].StateId("Активна").ToString() }, gate),
            () => DeleteAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal("Активна", deleted.Body.GetProperty("deletion").GetProperty("fromState").GetProperty("name").GetString());
    }

    // The delete first: the save answers 409 deleted and changes nothing.
    [Fact]
    public async Task Delete_Versus_Save_DeleteFirst_TheSaveAnswers409Deleted()
    {
        var (world, story) = await StoryAsync(deleted: false);

        var (deleted, saved) = await RaceAsync(
            gate => DeleteAsync(world.Items, story, gate: gate),
            () => WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { title = "Поздно" }));

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        LinksTestData.AssertRefused(saved, HttpStatusCode.Conflict, Deleted);
        Assert.Equal("История", (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("title").GetString());
    }

    // --- Delete ∥ Restore, both orders ------------------------------------------------------------------------------------

    // The delete first: the restore queues on the item's row, finds it deleted under its own lock and brings it back to the held state.
    [Fact]
    public async Task Delete_Versus_Restore_DeleteFirst_TheRestoreFindsTheItemDeletedAndBringsItBack()
    {
        var (world, story) = await StoryAsync(deleted: false, state: "Активна");

        var (deleted, restored) = await RaceAsync(gate => DeleteAsync(world.Items, story, gate: gate), () => RestoreAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(("InProgress", JsonValueKind.Null), (CategoryOf(item), item.GetProperty("deletion").ValueKind));
    }

    // The restore first: the delete queues on the row, finds the item live under its own lock and deletes it again, holding the state
    // the restore set.
    [Fact]
    public async Task Delete_Versus_Restore_RestoreFirst_TheDeleteFindsTheItemLiveAndDeletesItAgain()
    {
        var (world, story) = await StoryAsync(deleted: true, state: "Активна");

        var (restored, deleted) = await RaceAsync(gate => RestoreAsync(world.Items, story, gate), () => DeleteAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal("Removed", CategoryOf(item));
        Assert.Equal("Активна", item.GetProperty("deletion").GetProperty("fromState").GetProperty("name").GetString());
    }

    // --- Restore ∥ Restore / Save -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Restore_Versus_Restore()
    {
        var (world, story) = await StoryAsync(deleted: true, state: "Активна");

        var (a, b) = await RaceAsync(gate => RestoreAsync(world.Items, story, gate), () => RestoreAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.OK, a.Status);
        LinksTestData.AssertRefused(b, HttpStatusCode.Conflict, NotDeleted);
        Assert.Equal("InProgress", CategoryOf(await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)));
    }

    // The restore first: a save decided second lands on the restored item.
    [Fact]
    public async Task Restore_Versus_Save_RestoreFirst_TheSaveLandsOnTheRestoredItem()
    {
        var (world, story) = await StoryAsync(deleted: true);

        var (restored, saved) = await RaceAsync(
            gate => RestoreAsync(world.Items, story, gate),
            () => WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { title = "После восстановления" }));

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(HttpStatusCode.OK, saved.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(("После восстановления", "Proposed"), (item.GetProperty("title").GetString(), CategoryOf(item)));
    }

    // The save first: a save of a deleted item is refused under the item's lock, before it writes anything, so it never reaches a
    // commit gate and cannot be held there. The order is by sequence: the save answers 409 deleted, the restore that follows finds
    // the item exactly as it was deleted (BR-DR-11: one order, no half state).
    [Fact]
    public async Task Restore_Versus_Save_SaveFirst_TheSaveIsRefused_TheRestoreFindsTheItemUntouched()
    {
        var (world, story) = await StoryAsync(deleted: true);

        var saved = await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { title = "Слишком рано" });
        var restored = await RestoreAsync(world.Items, story);

        LinksTestData.AssertRefused(saved, HttpStatusCode.Conflict, Deleted);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(("История", "Proposed"), (item.GetProperty("title").GetString(), CategoryOf(item)));
    }

    // --- Restore ∥ Purge, both orders -------------------------------------------------------------------------------------

    // The restore first: the purge's early read still sees the item deleted, then waits on its row and finds it live: 409.
    [Fact]
    public async Task Restore_Versus_Purge_RestoreFirst_ThePurgeAnswers409NotDeleted()
    {
        var (world, story) = await StoryAsync(deleted: true);

        var (restored, purged) = await RaceAsync(gate => RestoreAsync(world.Items, story, gate), () => PurgeAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        LinksTestData.AssertRefused(purged, HttpStatusCode.Conflict, NotDeleted);
        Assert.Equal("Proposed", CategoryOf(await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)));
    }

    // The purge first: the restore finds no row.
    [Fact]
    public async Task Restore_Versus_Purge_PurgeFirst_TheRestoreAnswers404()
    {
        var (world, story) = await StoryAsync(deleted: true);

        var (purged, restored) = await RaceAsync(gate => PurgeAsync(world.Items, story, gate), () => RestoreAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        LinksTestData.AssertRefused(restored, HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // --- Purge ∥ Purge, Delete ∥ Purge ------------------------------------------------------------------------------------

    [Fact]
    public async Task Purge_Versus_Purge()
    {
        var (world, story) = await StoryAsync(deleted: true);

        var (a, b) = await RaceAsync(gate => PurgeAsync(world.Items, story, gate), () => PurgeAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.NoContent, a.Status);
        LinksTestData.AssertRefused(b, HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // --- Purge ∥ Save of the item itself, both orders ---------------------------------------------------------------------

    // The purge first: the save queues on the item's row and, after the commit, finds no row.
    [Fact]
    public async Task Purge_Versus_Save_PurgeFirst_TheSaveAnswers404()
    {
        var (world, story) = await StoryAsync(deleted: true);

        var (purged, saved) = await RaceAsync(
            gate => PurgeAsync(world.Items, story, gate),
            () => WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { title = "Поздно" }));

        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        LinksTestData.AssertRefused(saved, HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // The save first: refused under the lock, so (as with the restore) there is no commit to hold; the purge that follows finds the
    // item as it was deleted and removes it.
    [Fact]
    public async Task Purge_Versus_Save_SaveFirst_TheSaveIsRefused_ThePurgeThenGoesThrough()
    {
        var (world, story) = await StoryAsync(deleted: true);

        var saved = await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { title = "Слишком рано" });
        var purged = await PurgeAsync(world.Items, story);

        LinksTestData.AssertRefused(saved, HttpStatusCode.Conflict, Deleted);
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, story.Identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // The delete not yet committed: the purge's early read sees a live item and answers 409 without waiting; after the commit it goes through.
    [Fact]
    public async Task Delete_Versus_Purge_ThePurgeOfAnItemBeingDeletedAnswers409NotDeleted_ThenGoesThrough()
    {
        var (world, story) = await StoryAsync(deleted: false);

        var (deleted, early) = await RaceAsync(gate => DeleteAsync(world.Items, story, gate: gate), () => PurgeAsync(world.Items, story), secondWaits: false);

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        LinksTestData.AssertRefused(early, HttpStatusCode.Conflict, NotDeleted);
        Assert.Equal(HttpStatusCode.NoContent, (await PurgeAsync(world.Items, story)).Status);
    }

    // --- A child's link writes against a purge of its parent ---------------------------------------------------------------

    /// <summary>A deleted feature F with a live story K beneath it (and K's link kept).</summary>
    private async Task<(BoardsWorld World, Item Parent, Item Child)> ParentWithChildAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var parent = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var child = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, child, parent);
        await LinksTestData.DeleteAsync(factory, world.Items, parent);

        return (world, parent, child);
    }

    // Restore ∥ a child's mapping: the restore first — the mapping, which saw no parent (the parent was deleted), finds it live and
    // answers parent_changed (BR-H-4).
    [Fact]
    public async Task Restore_Versus_AChildsMapping_RestoreFirst_Answers_ParentChanged()
    {
        var (world, parent, child) = await ParentWithChildAsync();
        var other = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Другая фича", world.PlatformAreaId);

        var (restored, mapped) = await RaceAsync(
            gate => RestoreAsync(world.Items, parent, gate),
            () => LinksTestData.SetParentAsync(factory, world.Items, child.Identifier, other.Id, null));

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        LinksTestData.AssertRefused(mapped, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        Assert.Equal(parent.Id, await LinksTestData.StoredParentAsync(factory, child));
    }

    // The mapping first: the child is re-parented, the deleted parent is untouched, and the restore then proceeds.
    [Fact]
    public async Task Restore_Versus_AChildsMapping_MappingFirst_TheChildMoves_ThenTheParentIsRestored()
    {
        var (world, parent, child) = await ParentWithChildAsync();
        var other = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Другая фича", world.PlatformAreaId);

        var (mapped, restored) = await RaceAsync(
            gate => LinksTestData.SetParentAsync(factory, world.Items, child.Identifier, other.Id, null, gate),
            () => RestoreAsync(world.Items, parent));

        Assert.Equal(HttpStatusCode.OK, mapped.Status);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(other.Id, await LinksTestData.StoredParentAsync(factory, child));
        Assert.Equal("Proposed", CategoryOf(await WorkItemsTestData.ReadAsync(factory, world.Items, parent.Identifier)));
    }

    // Purge ∥ a child's release, the purge first: the release finds the child already unparented — its seen parent no longer matches.
    [Fact]
    public async Task Purge_Versus_AChildsRelease_PurgeFirst_TheReleaseFindsTheChildUnparented()
    {
        var (world, parent, child) = await ParentWithChildAsync();

        var (purged, released) = await RaceAsync(gate => PurgeAsync(world.Items, parent, gate), () => ReleaseLinkAsync(world.Items, child, parent));

        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        LinksTestData.AssertRefused(released, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        Assert.Null(await LinksTestData.StoredParentAsync(factory, child));
    }

    // The release first: the purge, queued on the hierarchy, then finds nothing to clear and removes the parent.
    [Fact]
    public async Task Purge_Versus_AChildsRelease_ReleaseFirst_ThePurgeThenFindsNothingToClear()
    {
        var (world, parent, child) = await ParentWithChildAsync();

        var (released, purged) = await RaceAsync(gate => ReleaseLinkAsync(world.Items, child, parent, gate), () => PurgeAsync(world.Items, parent));

        Assert.Equal(HttpStatusCode.OK, released.Status);
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, child));
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, parent.Identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // Purge ∥ a mapping naming the purged item as a parent, and a creation beneath it: the purge first — 404 parent_not_found.
    [Fact]
    public async Task Purge_Versus_AMappingNamingTheParent_AndACreationBeneath_PurgeFirst_Answer404ParentNotFound()
    {
        var (world, parent, _) = await ParentWithChildAsync();
        var other = await BoardsTestData.CreateAsync(factory, world, "US", "Другая история", world.PlatformAreaId);

        var (purged, mapped) = await RaceAsync(gate => PurgeAsync(world.Items, parent, gate), () => LinksTestData.SetParentAsync(factory, world.Items, other.Identifier, parent.Id, null));

        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        LinksTestData.AssertRefused(mapped, HttpStatusCode.NotFound, "workitems.link.parent_not_found");

        var (world2, parent2, _) = await ParentWithChildAsync();
        var (purged2, created) = await RaceAsync(
            gate => PurgeAsync(world2.Items, parent2, gate),
            () => BacklogTreeTestData.CreateChildAsync(factory, world2, "US", "Под очищенной", parent2.Id.ToString()));

        Assert.Equal(HttpStatusCode.NoContent, purged2.Status);
        LinksTestData.AssertRefused(created, HttpStatusCode.NotFound, "workitems.link.parent_not_found");
    }

    // A child's save against the purge of its parent: the save, first, holds the child; the purge waits for it, then clears the link.
    [Fact]
    public async Task AChildsSave_Versus_ParentPurge_TheSaveFirst_ThePurgeWaitsThenClearsTheLink()
    {
        var (world, parent, child) = await ParentWithChildAsync();

        var (saved, purged) = await RaceAsync(
            gate => WorkItemsTestData.SaveAsync(factory, world.Items, child.Identifier, new { title = "Новый заголовок" }, gate),
            () => PurgeAsync(world.Items, parent));

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, child.Identifier);
        Assert.Equal("Новый заголовок", item.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("parent").ValueKind);
    }

    // --- A team's deletion ------------------------------------------------------------------------------------------------

    // The team's deletion first: the purge waits on the owned rows and then deletes; the child ends unowned and unparented.
    [Fact]
    public async Task Purge_Versus_TeamDeletion_TeamFirst_ChildEndsUnownedAndUnparented()
    {
        var (world, parent, child) = await ParentWithChildAsync();

        var (teamDeleted, purged) = await RaceAsync(gate => DeleteTeamAsync(world, world.PlatformId, gate), () => PurgeAsync(world.Items, parent));

        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, child.Identifier);
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (item.GetProperty("team").ValueKind, item.GetProperty("parent").ValueKind));
    }

    // The purge first: the team's deletion waits on the rows the purge holds and then releases the surviving child.
    [Fact]
    public async Task Purge_Versus_TeamDeletion_PurgeFirst_ChildEndsUnownedAndUnparented()
    {
        var (world, parent, child) = await ParentWithChildAsync();

        var (purged, teamDeleted) = await RaceAsync(gate => PurgeAsync(world.Items, parent, gate), () => DeleteTeamAsync(world, world.PlatformId));

        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, child.Identifier);
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (item.GetProperty("team").ValueKind, item.GetProperty("parent").ValueKind));
    }

    // Delete ∥ a team's deletion, the team first: the delete waits for the row, then deletes an item that no team owns.
    [Fact]
    public async Task Delete_Versus_TeamDeletion_TeamFirst_TheDeletedItemIsUnowned()
    {
        var (world, story) = await StoryAsync(deleted: false);

        var (teamDeleted, deleted) = await RaceAsync(gate => DeleteTeamAsync(world, world.PlatformId, gate), () => DeleteAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(JsonValueKind.Null, deleted.Body.GetProperty("team").ValueKind);
    }

    // The delete first: the team's deletion waits, then releases the now-deleted item — the marks untouched, the owner cleared.
    [Fact]
    public async Task Delete_Versus_TeamDeletion_DeleteFirst_TheTeamDeletionReleasesTheDeletedItem_MarksUntouched()
    {
        var (world, story) = await StoryAsync(deleted: false, state: "Активна");

        var (deleted, teamDeleted) = await RaceAsync(gate => DeleteAsync(world.Items, story, gate: gate), () => DeleteTeamAsync(world, world.PlatformId));

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("team").ValueKind);
        Assert.Equal(deleted.Body.GetProperty("deletion").GetRawText(), item.GetProperty("deletion").GetRawText());
    }

    // --- Reorders ---------------------------------------------------------------------------------------------------------

    /// <summary>Two stories, ordered, in both backlogs.</summary>
    private async Task<(BoardsWorld World, Item Kept, Item Doomed)> OrderedPairAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var kept = await BoardsTestData.CreateAsync(factory, world, "US", "Остаётся", world.PlatformAreaId);
        var doomed = await BoardsTestData.CreateAsync(factory, world, "US", "Удаляется", world.PlatformAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, kept, doomed);
        await ProjectBoardsTestData.ReorderToAsync(factory, world, kept, doomed);

        return (world, kept, doomed);
    }

    // Delete ∥ a team's reorder, the reorder first: the delete waits for the members' key share; the place the reorder wrote is left
    // unread because a deleted item is no member.
    [Fact]
    public async Task Delete_Versus_TeamReorder_ReorderFirst_TheDeleteWaits_ThePlaceIsLeftUnread()
    {
        var (world, kept, doomed) = await OrderedPairAsync();

        var (reordered, deleted) = await RaceAsync(
            gate => BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Top(doomed), gate: gate),
            () => DeleteAsync(world.Items, doomed));

        Assert.Equal(HttpStatusCode.NoContent, reordered.Status);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal([kept.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // The delete first: the reorder's locking read no longer finds the item a member; a reorder that moves another item goes through.
    [Fact]
    public async Task Delete_Versus_TeamReorder_DeleteFirst_TheReorderNoLongerFindsTheItemAMember()
    {
        var (world, kept, doomed) = await OrderedPairAsync();

        var (deleted, reordered) = await RaceAsync(
            gate => DeleteAsync(world.Items, doomed, gate: gate),
            () => BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Top(kept)));

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(HttpStatusCode.NoContent, reordered.Status);
        Assert.Equal([kept.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // Delete ∥ the PROJECT's reorder (ADR-0030 "the finding"): the reorder first — the delete must wait for the members' key share,
    // which the project reorder took only with this task; the place the reorder wrote is left unread.
    [Fact]
    public async Task Delete_Versus_ProjectReorder_ReorderFirst_TheDeleteWaits()
    {
        var (world, kept, doomed) = await OrderedPairAsync();

        var (reordered, deleted) = await RaceAsync(
            gate => ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(doomed), gate: gate),
            () => DeleteAsync(world.Items, doomed));

        Assert.Equal(HttpStatusCode.NoContent, reordered.Status);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal([kept.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    [Fact]
    public async Task Delete_Versus_ProjectReorder_DeleteFirst_TheReorderNoLongerFindsTheItemAMember()
    {
        var (world, kept, doomed) = await OrderedPairAsync();

        var (deleted, reordered) = await RaceAsync(
            gate => DeleteAsync(world.Items, doomed, gate: gate),
            () => ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(kept)));

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(HttpStatusCode.NoContent, reordered.Status);
        Assert.Equal([kept.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // Purge ∥ a project reorder: a purge needs a prior Delete, so the item is no member; the reorder's drop of the item's stale place
    // and the purge's removal of it queue on the place row, with no cycle — both finish.
    [Fact]
    public async Task Purge_Versus_ProjectReorder_BothFinish_TheStalePlaceGoesOnce()
    {
        var (world, kept, doomed) = await OrderedPairAsync();
        await LinksTestData.DeleteAsync(factory, world.Items, doomed);

        var (reordered, purged) = await RaceAsync(
            gate => ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(kept), gate: gate),
            () => PurgeAsync(world.Items, doomed),
            secondWaits: false);

        Assert.Equal(HttpStatusCode.NoContent, reordered.Status);
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        Assert.Equal([kept.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(0, await OwningTeamTestData.ProjectPlacesOfAsync(factory, doomed.Id));
    }

    // The purge's own locks are not the reorder's: a reorder holds the key share of a sibling while a purge of its deleted parent runs,
    // and neither waits on the other (the purge locks its children NO KEY UPDATE, which a key share does not block).
    [Fact]
    public async Task Purge_DoesNotWaitOnAReordersKeyShareOfItsChild_AndTheReorderDoesNotWaitOnThePurge()
    {
        var (world, parent, child) = await ParentWithChildAsync();
        var sibling = await BoardsTestData.CreateAsync(factory, world, "US", "Соседняя", world.PlatformAreaId);
        await ProjectBoardsTestData.ReorderToAsync(factory, world, child, sibling);
        using var gate = factory.CommitGates.Open();

        var reorder = ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(sibling), gate: gate);
        await Task.WhenAny(gate.Reached, reorder);
        Assert.False(reorder.IsCompleted, "The reorder finished before reaching its commit gate.");

        // The purge finishes while the reorder still holds the key share of the child and the sibling.
        var purged = await PurgeAsync(world.Items, parent).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);

        gate.Release();
        Assert.Equal(HttpStatusCode.NoContent, (await reorder).Status);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, child));
    }

    // --- Creation beneath, mapping naming the item as a parent ------------------------------------------------------------

    // The creation first (it holds the parent FOR NO KEY UPDATE): the delete waits, the child is created and kept beneath the
    // deleted item (BR-DR-4).
    [Fact]
    public async Task Delete_Versus_CreationBeneath_CreationFirst_TheChildIsKeptBeneathTheDeletedParent()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);

        var (created, deleted) = await RaceAsync(
            gate => OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Post,
                WorkItemsTestData.List(world.Items),
                new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["US"].Id.ToString(), title = "Новая история", parentId = feature.Id.ToString() },
                OrganizationsTokens.SignedIn(),
                gate),
            () => DeleteAsync(world.Items, feature));

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(feature.Id, created.Body.GetProperty("parent").GetProperty("id").GetGuid());
        Assert.Equal("Removed", CategoryOf(await WorkItemsTestData.ReadAsync(factory, world.Items, feature.Identifier)));
    }

    // The delete first: the creation (and a mapping) naming the item as a parent answers item_deleted.
    [Fact]
    public async Task Delete_Versus_CreationBeneath_AndMappingNamingItAsParent_DeleteFirst_Answer409ItemDeleted()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);

        var (deleted, created) = await RaceAsync(
            gate => DeleteAsync(world.Items, feature, gate: gate),
            () => BacklogTreeTestData.CreateChildAsync(factory, world, "US", "Новая история", feature.Id.ToString()));

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        LinksTestData.AssertRefused(created, HttpStatusCode.Conflict, "workitems.link.item_deleted");

        var other = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Другая фича", world.PlatformAreaId);
        var (deleted2, mapped) = await RaceAsync(
            gate => DeleteAsync(world.Items, other, gate: gate),
            () => LinksTestData.SetParentAsync(factory, world.Items, story.Identifier, other.Id, null));

        Assert.Equal(HttpStatusCode.OK, deleted2.Status);
        LinksTestData.AssertRefused(mapped, HttpStatusCode.Conflict, "workitems.link.item_deleted");
        Assert.Null(await LinksTestData.StoredParentAsync(factory, story));
    }

    // --- A team's unlink and deletion against Delete, Restore and Purge (feat-028 merge; BR-DR-9, BR-DR-10, BR-DR-11) --------
    //
    // The clean-ups lock the items a team owns FOR UPDATE by id, in any state, so every pair is ordered by the item's row: the request
    // that comes second waits, then decides on what it finds. «Платформа» is the team that is unlinked or deleted; «Мастаск» is the
    // unlink's destination. A deleted item stays deleted whichever came first; its owner is whatever the clean-up left.

    /// <summary>A story «Платформа» owns, in «Удалена» when asked, and a live story it owns too (so the clean-up has more than one row).</summary>
    private async Task<(BoardsWorld World, Item Story, Item Live)> TeamStoryAsync(bool deleted, string state = "Новая")
    {
        var (world, story) = await StoryAsync(deleted, state);
        var live = await BoardsTestData.CreateAsync(factory, world, "US", "Живая", world.PlatformAreaId);

        return (world, story, live);
    }

    private async Task<Guid?> OwnerAsync(BoardsWorld world, Item item) =>
        (await OwningTeamTestData.StoredAsync(factory, world.ProjectId, item.Identifier)).TeamId;

    private async Task AssertConsistentAsync(BoardsWorld world) =>
        Assert.Equal(0, await OwningTeamTestData.PlacesNamingAnotherTeamAsync(factory, world.ProjectId));

    // Unlink ∥ Delete, the unlink first: the delete waits on the item's row, then deletes an item the destination owns.
    [Fact]
    public async Task Unlink_Versus_Delete_UnlinkFirst_TheDeleteWaits_ThenDeletesAnItemTheDestinationOwns()
    {
        var (world, story, live) = await TeamStoryAsync(deleted: false, state: "Активна");

        var (unlinked, deleted) = await RaceAsync(gate => UnlinkAsync(world, world.PlatformId, world.ProjectTeamId, gate), () => DeleteAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(world.ProjectTeamId, OwningTeamTestData.TeamOf(deleted.Body)?.Id);
        Assert.Equal("Активна", deleted.Body.GetProperty("deletion").GetProperty("fromState").GetProperty("name").GetString());
        Assert.Equal((world.ProjectTeamId, world.ProjectTeamId), (await OwnerAsync(world, story), await OwnerAsync(world, live)));
        await AssertConsistentAsync(world);
    }

    // The delete first: the unlink waits, then reassigns the item that is now in «Удалена»; its marks and state are the delete's.
    [Fact]
    public async Task Unlink_Versus_Delete_DeleteFirst_TheUnlinkWaits_ThenReassignsTheDeletedItem_MarksUntouched()
    {
        var (world, story, live) = await TeamStoryAsync(deleted: false, state: "Активна");

        var (deleted, unlinked) = await RaceAsync(gate => DeleteAsync(world.Items, story, gate: gate), () => UnlinkAsync(world, world.PlatformId, world.ProjectTeamId));

        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(("Removed", world.ProjectTeamId), (CategoryOf(item), OwningTeamTestData.TeamOf(item)?.Id));
        Assert.Equal(deleted.Body.GetProperty("deletion").GetRawText(), item.GetProperty("deletion").GetRawText());
        Assert.Equal(world.ProjectTeamId, await OwnerAsync(world, live));
        await AssertConsistentAsync(world);
    }

    // Unlink ∥ Restore, the unlink first: the restore waits, then brings the item back owned by the destination (BR-DR-10).
    [Fact]
    public async Task Unlink_Versus_Restore_UnlinkFirst_TheRestoreWaits_ThenBringsTheItemBackOwnedByTheDestination()
    {
        var (world, story, _) = await TeamStoryAsync(deleted: true, state: "Активна");

        var (unlinked, restored) = await RaceAsync(gate => UnlinkAsync(world, world.PlatformId, world.ProjectTeamId, gate), () => RestoreAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal((world.ProjectTeamId, "InProgress"), (OwningTeamTestData.TeamOf(restored.Body)?.Id, CategoryOf(restored.Body)));
        await AssertConsistentAsync(world);
    }

    // The restore first: the unlink waits, then reassigns the live item with the rest.
    [Fact]
    public async Task Unlink_Versus_Restore_RestoreFirst_TheUnlinkWaits_ThenReassignsTheRestoredItem()
    {
        var (world, story, live) = await TeamStoryAsync(deleted: true, state: "Активна");

        var (restored, unlinked) = await RaceAsync(gate => RestoreAsync(world.Items, story, gate), () => UnlinkAsync(world, world.PlatformId, world.ProjectTeamId));

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(("InProgress", world.ProjectTeamId), (CategoryOf(item), OwningTeamTestData.TeamOf(item)?.Id));
        Assert.Equal(world.ProjectTeamId, await OwnerAsync(world, live));
        await AssertConsistentAsync(world);
    }

    // Unlink ∥ Purge, the unlink first: the purge (its early read saw the item deleted) waits on the row, then removes the item the
    // destination now owns; the live item went with the unlink.
    [Fact]
    public async Task Unlink_Versus_Purge_UnlinkFirst_ThePurgeWaits_ThenRemovesTheReassignedItem()
    {
        var (world, story, live) = await TeamStoryAsync(deleted: true);

        var (unlinked, purged) = await RaceAsync(gate => UnlinkAsync(world, world.PlatformId, world.ProjectTeamId, gate), () => PurgeAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, story.Identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        Assert.Equal(world.ProjectTeamId, await OwnerAsync(world, live));
        await AssertConsistentAsync(world);
    }

    // The purge first: the unlink waits on the item's row, then finds it gone and reassigns what is left.
    [Fact]
    public async Task Unlink_Versus_Purge_PurgeFirst_TheUnlinkWaits_ThenReassignsWhatIsLeft()
    {
        var (world, story, live) = await TeamStoryAsync(deleted: true);

        var (purged, unlinked) = await RaceAsync(gate => PurgeAsync(world.Items, story, gate), () => UnlinkAsync(world, world.PlatformId, world.ProjectTeamId));

        Assert.Equal(HttpStatusCode.NoContent, purged.Status);
        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, story.Identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        Assert.Equal(world.ProjectTeamId, await OwnerAsync(world, live));
        await AssertConsistentAsync(world);
    }

    // A team's deletion ∥ Restore, the deletion first: the restore waits, then brings back an item that no team owns (BR-DR-10).
    [Fact]
    public async Task TeamDeletion_Versus_Restore_DeletionFirst_TheRestoreWaits_ThenBringsTheItemBackOwnedByNobody()
    {
        var (world, story, live) = await TeamStoryAsync(deleted: true, state: "Активна");

        var (teamDeleted, restored) = await RaceAsync(gate => DeleteTeamAsync(world, world.PlatformId, gate), () => RestoreAsync(world.Items, story));

        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal((JsonValueKind.Null, "InProgress"), (restored.Body.GetProperty("team").ValueKind, CategoryOf(restored.Body)));
        Assert.Null(await OwnerAsync(world, live));
        await AssertConsistentAsync(world);
    }

    // The restore first: the deletion waits, then releases the restored (live) item with the rest.
    [Fact]
    public async Task TeamDeletion_Versus_Restore_RestoreFirst_TheDeletionWaits_ThenReleasesTheRestoredItem()
    {
        var (world, story, live) = await TeamStoryAsync(deleted: true, state: "Активна");

        var (restored, teamDeleted) = await RaceAsync(gate => RestoreAsync(world.Items, story, gate), () => DeleteTeamAsync(world, world.PlatformId));

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal((JsonValueKind.Null, "InProgress"), (item.GetProperty("team").ValueKind, CategoryOf(item)));
        Assert.Null(await OwnerAsync(world, live));
        await AssertConsistentAsync(world);
    }

    // --- The project row against the items (the one lock-order cycle the merge made) --------------------------------------------
    //
    // A project backlog's reorder takes the project row FOR SHARE first and then its members FOR KEY SHARE (mod-005 plan-backend
    // § 7.5); an unlink or a deletion of the project's named default team takes the team's items FOR UPDATE and clears the setting
    // on the project row (feat-028 plan-backend § 7.2). In that order each holds what the other wants. The cycle is built here
    // without a race: a session of the test holds the project row FOR SHARE as the reorder would, the clean-up runs until it blocks
    // on that row, the reorder starts and, in the order that has the cycle, blocks on the clean-up's items — and only then the
    // project row is released. The two ends must both finish; the project row comes before the items, whatever the order of
    // arrival (plan-backend § 14).

    private async Task<(Answer CleanUp, Answer Reorder)> CleanUpVersusProjectReorderAsync(BoardsWorld world, Func<Task<Answer>> cleanUp, Func<Task<Answer>> reorder)
    {
        await using var holder = new NpgsqlConnection(factory.ConnectionString);
        await holder.OpenAsync();
        await using var transaction = await holder.BeginTransactionAsync();

        await using (var command = new NpgsqlCommand("SELECT 1 FROM projects WHERE id = @project FOR SHARE", holder, transaction))
        {
            command.Parameters.AddWithValue("project", world.ProjectId);
            await command.ExecuteScalarAsync();
        }

        var cleanUpTask = cleanUp();
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, cleanUpTask);
        Assert.False(cleanUpTask.IsCompleted, "The clean-up did not wait for the project row.");

        var reorderTask = reorder();

        // Two sessions blocked together is the cycle's hold-and-wait; in the order without a cycle the reorder is not blocked at all.
        await Task.WhenAny(reorderTask, PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, 2));

        await transaction.CommitAsync();

        return (await cleanUpTask, await reorderTask);
    }

    /// <summary>Two stories «Мастаск» — the project's named default team, which owns them — ordered in the project backlog.</summary>
    private async Task<(BoardsWorld World, Item First, Item Second)> DefaultTeamStoriesAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая");
        var second = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая");
        await ProjectBoardsTestData.ReorderToAsync(factory, world, first, second);
        Assert.Equal(world.ProjectTeamId, await TeamTestData.SettingAsync(factory, world.ProjectId));
        Assert.Equal((world.ProjectTeamId, world.ProjectTeamId), (await OwnerAsync(world, first), await OwnerAsync(world, second)));

        return (world, first, second);
    }

    // The unlink of the named default team against a project reorder: both finish, the setting is cleared, the order is the reorder's.
    [Fact]
    public async Task Unlink_OfTheNamedDefault_Versus_ProjectReorder_NoCycle_BothFinish()
    {
        var (world, first, second) = await DefaultTeamStoriesAsync();

        var (unlinked, reordered) = await CleanUpVersusProjectReorderAsync(
            world,
            () => UnlinkAsync(world, world.ProjectTeamId, world.PlatformId),
            () => ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(second)));

        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        Assert.Equal(HttpStatusCode.NoContent, reordered.Status);
        Assert.Null(await TeamTestData.SettingAsync(factory, world.ProjectId));
        Assert.Equal([second.Identifier, first.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal((world.PlatformId, world.PlatformId), (await OwnerAsync(world, first), await OwnerAsync(world, second)));
    }

    // The same for a deletion of the named default team, which clears the setting of every project that names it.
    [Fact]
    public async Task TeamDeletion_OfTheNamedDefault_Versus_ProjectReorder_NoCycle_BothFinish()
    {
        var (world, first, second) = await DefaultTeamStoriesAsync();

        var (teamDeleted, reordered) = await CleanUpVersusProjectReorderAsync(
            world,
            () => DeleteTeamAsync(world, world.ProjectTeamId),
            () => ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(second)));

        Assert.Equal(HttpStatusCode.NoContent, teamDeleted.Status);
        Assert.Equal(HttpStatusCode.NoContent, reordered.Status);
        Assert.Null(await TeamTestData.SettingAsync(factory, world.ProjectId));
        Assert.Equal([second.Identifier, first.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Null(await OwnerAsync(world, first));
        Assert.Null(await OwnerAsync(world, second));
    }
}
