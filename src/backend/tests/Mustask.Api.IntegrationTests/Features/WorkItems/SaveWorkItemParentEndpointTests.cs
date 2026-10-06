using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>PATCH /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}</c>
/// with feat-001's <c>parent</c> (its api-contract § 3.3): the form's «Родитель»
/// saved with every other changed field, all of them or none (BR-H-6), by the
/// same rules as the parent write (US-1, US-2, US-7), and the save's order of
/// answers with the parent's codes in their places.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SaveWorkItemParentEndpointTests(MustaskApiFactory factory)
{
    private async Task<(WorkItemsWorld World, IReadOnlyDictionary<string, Item> Items)> WorldAsync(int upTo = 8)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        return (world, await LinksTestData.CreateItemsAsync(factory, world, upTo));
    }

    private static object Parent(Item? parent, Item? seen) => new { parentId = parent?.Id.ToString(), seenParentId = seen?.Id.ToString() };

    // --- US-1 -------------------------------------------------------------------------------------------------------

    // US-1 · "The parent is saved with the other fields, all or none" and "A parent is set and saved": one save stores
    // Заголовок and «Родитель», and answers the item with both; after a reload it reads the same.
    [Fact]
    public async Task Patch_TitleAndParent_BothStoredByOneSave()
    {
        var (world, items) = await WorldAsync();

        var answer = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { title = "Письмо со ссылкой на вход", parent = Parent(items["MSK-FEAT-6"], null) });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(("Письмо со ссылкой на вход", "MSK-FEAT-6"), (answer.Body.GetProperty("title").GetString(), LinksTestData.IdentifierOf(answer.Body.GetProperty("parent"))));
        var reloaded = await WorkItemsTestData.ReadAsync(factory, world, "MSK-US-2");
        Assert.Equal(("Письмо со ссылкой на вход", "MSK-FEAT-6"), (reloaded.GetProperty("title").GetString(), LinksTestData.IdentifierOf(reloaded.GetProperty("parent"))));
    }

    // US-1 · "The parent is saved with the other fields, all or none", the refusal: the server refuses the parent, so
    // neither field is stored and the code names the parent's rule.
    [Fact]
    public async Task Patch_TitleAndARefusedParent_NeitherStored()
    {
        var (world, items) = await WorldAsync();
        var before = await LinksTestData.StoredAsync(factory, items["MSK-US-2"]);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { title = "Другой заголовок", parent = Parent(items["MSK-TASK-3"], null) });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.parent_level_not_allowed");
        Assert.Equal("Письмо со ссылкой", (await WorkItemsTestData.ReadAsync(factory, world, "MSK-US-2")).GetProperty("title").GetString());
        Assert.Equal(before, await LinksTestData.StoredAsync(factory, items["MSK-US-2"]));
    }

    // US-1 · "Choosing another parent replaces the stored one in one save" (OQ-2): exactly one parent; the old one no
    // longer lists the child; «Изменён» moved on all three.
    [Fact]
    public async Task Patch_AnotherParent_ReplacesIt()
    {
        var (world, items) = await WorldAsync();
        var (story, feature6, feature8) = (items["MSK-US-2"], items["MSK-FEAT-6"], items["MSK-FEAT-8"]);
        await LinksTestData.LinkAsync(factory, world, story, feature6);
        var before = (await LinksTestData.StoredAsync(factory, feature6)).ChangedAt;

        var answer = await WorkItemsTestData.SaveAsync(factory, world, story.Identifier, new { parent = Parent(feature8, feature6) });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("MSK-FEAT-8", LinksTestData.IdentifierOf(answer.Body.GetProperty("parent")));
        Assert.Empty(LinksTestData.Identifiers((await WorkItemsTestData.ReadAsync(factory, world, feature6.Identifier)).GetProperty("children")));
        var changed = answer.Body.GetProperty("changedAt").GetDateTimeOffset();
        Assert.True(changed > before);
        Assert.Equal((changed, changed), ((await LinksTestData.StoredAsync(factory, feature6)).ChangedAt, (await LinksTestData.StoredAsync(factory, feature8)).ChangedAt));
    }

    // US-1 · "«Нет родителя» clears the parent".
    [Fact]
    public async Task Patch_NoParent_ClearsIt()
    {
        var (world, items) = await WorldAsync();
        await LinksTestData.LinkAsync(factory, world, items["MSK-US-2"], items["MSK-FEAT-6"]);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { parent = Parent(null, items["MSK-FEAT-6"]) });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("parent").ValueKind);
        Assert.Empty(LinksTestData.Identifiers((await WorkItemsTestData.ReadAsync(factory, world, "MSK-FEAT-6")).GetProperty("children")));
    }

    // US-1 · "A save of other fields leaves the parent alone": Анна's form loaded with no parent, Борис has since given it
    // MSK-FEAT-6; Анна saves only Исполнитель and Заголовок, without `parent`, or with `"parent": null`.
    [Fact]
    public async Task Patch_WithoutParent_LeavesTheParentWhoeverSetIt()
    {
        var (world, items) = await WorldAsync();
        var anna = await WorkItemsTestData.AddUserAsync(factory, "Анна", "Смирнова");
        await LinksTestData.LinkAsync(factory, world, items["MSK-US-2"], items["MSK-FEAT-6"]);

        var assigned = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { assignee = new { userId = anna } });
        var retitled = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { title = "Письмо", parent = (object?)null });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (assigned.Status, retitled.Status));
        Assert.Equal(items["MSK-FEAT-6"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-US-2"]));
        Assert.Equal("MSK-FEAT-6", LinksTestData.IdentifierOf(retitled.Body.GetProperty("parent")));
    }

    // --- US-2 -------------------------------------------------------------------------------------------------------

    // US-2 · "A parent chosen from a stale view is refused": Анна picks MSK-FEAT-6 and changes Заголовок; Борис has since
    // given MSK-FEAT-8. Nothing of Анна's save is stored; the parent is still MSK-FEAT-8.
    [Fact]
    public async Task Patch_ParentFromAStaleView_Returns409ParentChanged_NothingStored()
    {
        var (world, items) = await WorldAsync();
        await LinksTestData.LinkAsync(factory, world, items["MSK-US-2"], items["MSK-FEAT-8"]);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { title = "Анна", parent = Parent(items["MSK-FEAT-6"], null) });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        var stored = await WorkItemsTestData.ReadAsync(factory, world, "MSK-US-2");
        Assert.Equal(("Письмо со ссылкой", "MSK-FEAT-8"), (stored.GetProperty("title").GetString(), LinksTestData.IdentifierOf(stored.GetProperty("parent"))));
    }

    // US-2 · "A cycle is refused, naming its path", through the form: under «Родитель» reads the code's text.
    [Fact]
    public async Task Patch_ADescendant_Returns409Cycle_WithItsPath()
    {
        var (world, items) = await WorldAsync(upTo: 12);
        await LinksTestData.LinkAsync(factory, world, items["MSK-TASK-11"], items["MSK-TASK-10"]);
        await LinksTestData.LinkAsync(factory, world, items["MSK-TASK-12"], items["MSK-TASK-11"]);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, "MSK-TASK-10", new { parent = Parent(items["MSK-TASK-12"], null) });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.cycle");
        Assert.Equal("Нельзя выбрать родителем MSK-TASK-12: он вложен в MSK-TASK-10 (MSK-TASK-10 → MSK-TASK-11 → MSK-TASK-12).", answer.Detail);
    }

    // US-2 · "An item of another project is refused as not found", through the form.
    [Fact]
    public async Task Patch_AParentOfAnotherProject_Returns404ParentNotFound()
    {
        var (world, _) = await WorldAsync(upTo: 6);
        var warehouse = await WorkItemsTestData.AddProjectAsync(factory, world, "WH", "Склад");
        var whEpic = await WorkItemsTestData.CreateAsync(factory, warehouse, "EPIC", "Склад: учёт");

        var answer = await WorkItemsTestData.SaveAsync(factory, world, "MSK-FEAT-6", new { parent = new { parentId = whEpic.Body.GetProperty("id").GetString() } });

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "workitems.link.parent_not_found");
    }

    // api-contract § 3.3, the order of answers: the values' 400 before the parent's not-found; the area's not-found
    // before the parent's; the parent's not-found before the state's 409; the state's 409 before the parent's.
    [Fact]
    public async Task Patch_SeveralFaults_TheSavesOrderOfAnswers()
    {
        var (world, items) = await WorldAsync();
        var unknownParent = new { parentId = Guid.CreateVersion7().ToString() };
        var taskParent = Parent(items["MSK-TASK-3"], null);
        var foreignState = world.Types["BUG"].StateId("Активна").ToString();

        var values = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { title = "", parent = unknownParent });
        var area = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { areaId = Guid.CreateVersion7().ToString(), parent = unknownParent });
        var parent = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { stateId = foreignState, parent = unknownParent });
        var state = await WorkItemsTestData.SaveAsync(factory, world, "MSK-US-2", new { stateId = foreignState, parent = taskParent });

        LinksTestData.AssertRefused(values, HttpStatusCode.BadRequest, "workitems.validation_failed");
        LinksTestData.AssertRefused(area, HttpStatusCode.NotFound, "organizations.area.not_found");
        LinksTestData.AssertRefused(parent, HttpStatusCode.NotFound, "workitems.link.parent_not_found");
        LinksTestData.AssertRefused(state, HttpStatusCode.Conflict, "workitems.workitem.state_not_allowed");
    }

    // --- US-7 -------------------------------------------------------------------------------------------------------

    // US-7 · "The child's form lets go of a deleted parent": the form shows MSK-EPIC-1 with «Удалена»; a save of «Нет
    // родителя» that names it as seen clears it, and MSK-EPIC-1 is unchanged, «Изменён» included.
    [Fact]
    public async Task Patch_NoParent_OverADeletedParent_ClearsIt_TheDeletedParentUntouched()
    {
        var (world, items) = await WorldAsync();
        var (epic, feature) = (items["MSK-EPIC-1"], items["MSK-FEAT-8"]);
        await LinksTestData.LinkAsync(factory, world, feature, epic);
        await LinksTestData.DeleteAsync(factory, world, epic);
        var deleted = await LinksTestData.StoredAsync(factory, epic);
        var form = await WorkItemsTestData.ReadAsync(factory, world, feature.Identifier);
        Assert.Equal(("MSK-EPIC-1", "Удалена"), (LinksTestData.IdentifierOf(form.GetProperty("parent")), form.GetProperty("parent").GetProperty("state").GetProperty("name").GetString()));

        var answer = await WorkItemsTestData.SaveAsync(factory, world, feature.Identifier, new { parent = Parent(null, epic) });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, feature));
        Assert.Equal(deleted, await LinksTestData.StoredAsync(factory, epic));
    }
}
