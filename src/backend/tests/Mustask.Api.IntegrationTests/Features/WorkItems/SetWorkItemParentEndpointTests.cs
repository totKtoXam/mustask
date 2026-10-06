using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent</c>
/// (feat-001 api-contract § 3.2): one parent write — set, replace in one write,
/// or clear — with its refusals in the spec's order (US-1, US-2, US-7), the
/// project's parent writes queued on its hierarchy lock, and the address resolved
/// as mod-003's read resolves it.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SetWorkItemParentEndpointTests(MustaskApiFactory factory)
{
    private const string ParentChangedText = "Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.";

    /// <summary>US-1's data in «Мастаск» (MSK), items 1..<paramref name="upTo"/>.</summary>
    private async Task<(WorkItemsWorld World, IReadOnlyDictionary<string, Item> Items)> WorldAsync(int upTo = 8)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        return (world, await LinksTestData.CreateItemsAsync(factory, world, upTo));
    }

    // --- US-1 -------------------------------------------------------------------------------------------------------

    // US-1 · "A parent is set and saved" (BR-H-3, BR-H-5): 200 with the parent as stored; the form reads it; «Изменён»
    // moved on the story and on the feature.
    [Fact]
    public async Task Put_Parent_Returns200_WithTheStoredParent_ChangedMovesOnBoth()
    {
        var (world, items) = await WorldAsync();
        var (story, feature) = (items["MSK-US-2"], items["MSK-FEAT-6"]);
        var before = (await LinksTestData.StoredAsync(factory, story)).ChangedAt;

        var answer = await LinksTestData.SetParentAsync(factory, world, story.Identifier, feature.Id, null);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var parent = answer.Body.GetProperty("parent");
        Assert.Equal(("MSK-FEAT-6", "Вход по ссылке", "FEAT", "Новая"), (
            parent.GetProperty("identifier").GetString(),
            parent.GetProperty("title").GetString(),
            parent.GetProperty("type").GetProperty("key").GetString(),
            parent.GetProperty("state").GetProperty("name").GetString()));
        Assert.Equal(feature.Id, parent.GetProperty("id").GetGuid());

        var form = await WorkItemsTestData.ReadAsync(factory, world, story.Identifier);
        Assert.Equal("MSK-FEAT-6", LinksTestData.IdentifierOf(form.GetProperty("parent")));
        var (storyAfter, featureAfter) = (await LinksTestData.StoredAsync(factory, story), await LinksTestData.StoredAsync(factory, feature));
        Assert.True(storyAfter.ChangedAt > before);
        Assert.Equal(storyAfter.ChangedAt, featureAfter.ChangedAt);
    }

    // US-1 · "Choosing another parent replaces the stored one in one write": exactly one parent; the old one no longer
    // lists the child (US-3); «Изменён» moved on all three to the write's time.
    [Fact]
    public async Task Put_AnotherParent_ReplacesIt_TheOldOneLetsGo_ChangedMovesOnAllThree()
    {
        var (world, items) = await WorldAsync();
        var (story, oldParent, newParent) = (items["MSK-US-2"], items["MSK-FEAT-6"], items["MSK-FEAT-8"]);
        await LinksTestData.LinkAsync(factory, world, story, oldParent);

        var answer = await LinksTestData.SetParentAsync(factory, world, story.Identifier, newParent.Id, oldParent.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("MSK-FEAT-8", LinksTestData.IdentifierOf(answer.Body.GetProperty("parent")));
        Assert.Equal(newParent.Id, await LinksTestData.StoredParentAsync(factory, story));
        Assert.Empty(LinksTestData.Identifiers((await WorkItemsTestData.ReadAsync(factory, world, oldParent.Identifier)).GetProperty("children")));
        Assert.Equal(["MSK-US-2"], LinksTestData.Identifiers((await WorkItemsTestData.ReadAsync(factory, world, newParent.Identifier)).GetProperty("children")));

        var times = new List<DateTimeOffset>();

        foreach (var item in new[] { story, oldParent, newParent })
        {
            times.Add((await LinksTestData.StoredAsync(factory, item)).ChangedAt);
        }

        Assert.Single(times.Distinct());
    }

    // US-1 · "«Нет родителя» clears the parent", US-5 · "«Убрать родителя» clears the parent": 200 with a null parent; the
    // feature lists no child.
    [Fact]
    public async Task Put_NoParent_ClearsIt_Returns200WithNull()
    {
        var (world, items) = await WorldAsync();
        var (story, feature) = (items["MSK-US-2"], items["MSK-FEAT-6"]);
        await LinksTestData.LinkAsync(factory, world, story, feature);

        var answer = await LinksTestData.SetParentAsync(factory, world, story.Identifier, null, feature.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, answer.Body.GetProperty("parent").ValueKind);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, story));
        Assert.Empty(LinksTestData.Identifiers((await WorkItemsTestData.ReadAsync(factory, world, feature.Identifier)).GetProperty("children")));
    }

    // BR-H-3: the stored parent named again, or a clear with no parent stored (a missing body included), changes
    // nothing, «Изменён» included.
    [Fact]
    public async Task Put_TheStoredParentAgainOrNothingToClear_Returns200_ChangesNothing()
    {
        var (world, items) = await WorldAsync();
        var (story, feature, bug) = (items["MSK-US-2"], items["MSK-FEAT-6"], items["MSK-BUG-4"]);
        await LinksTestData.LinkAsync(factory, world, story, feature);
        var before = (await LinksTestData.StoredAsync(factory, story), await LinksTestData.StoredAsync(factory, feature), await LinksTestData.StoredAsync(factory, bug));

        var again = await LinksTestData.SetParentAsync(factory, world, story.Identifier, feature.Id, feature.Id);
        var nothing = await OrganizationsApi.SendAsync(factory, HttpMethod.Put, LinksTestData.Parent(world, bug.Identifier), token: OrganizationsTokens.SignedIn());

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (again.Status, nothing.Status));
        Assert.Equal(before, (await LinksTestData.StoredAsync(factory, story), await LinksTestData.StoredAsync(factory, feature), await LinksTestData.StoredAsync(factory, bug)));
    }

    // US-1 · "A link changes no other field and no backlog place" (BR-H-5): Статус, Исполнитель, Область unchanged.
    [Fact]
    public async Task Put_Parent_ChangesNoOtherField()
    {
        var (world, items) = await WorldAsync();
        var story = items["MSK-US-2"];
        await WorkItemsTestData.SaveAsync(factory, world, story.Identifier, new { stateId = world.Types["US"].StateId("Активна").ToString() });
        var before = await WorkItemsTestData.ReadAsync(factory, world, story.Identifier);

        await LinksTestData.LinkAsync(factory, world, story, items["MSK-FEAT-6"]);

        var after = await WorkItemsTestData.ReadAsync(factory, world, story.Identifier);
        foreach (var field in new[] { "title", "description", "state", "assignee", "area", "createdAt" })
        {
            Assert.Equal(before.GetProperty(field).GetRawText(), after.GetProperty(field).GetRawText());
        }
    }

    // --- US-2 -------------------------------------------------------------------------------------------------------

    // US-2 · "A parent on a lower level is refused" and "A refused replace keeps the old parent".
    [Fact]
    public async Task Put_ALowerLevel_Returns409LevelNotAllowed_TheOldParentStays()
    {
        var (world, items) = await WorldAsync();
        var (story, feature, task) = (items["MSK-US-2"], items["MSK-FEAT-6"], items["MSK-TASK-3"]);

        var unparented = await LinksTestData.SetParentAsync(factory, world, story.Identifier, task.Id, null);
        await LinksTestData.LinkAsync(factory, world, story, feature);
        var replacing = await LinksTestData.SetParentAsync(factory, world, story.Identifier, task.Id, feature.Id);

        LinksTestData.AssertRefused(unparented, HttpStatusCode.Conflict, "workitems.link.parent_level_not_allowed");
        Assert.Equal("Родителем может быть только рабочий элемент того же или более высокого уровня.", unparented.Detail);
        LinksTestData.AssertRefused(replacing, HttpStatusCode.Conflict, "workitems.link.parent_level_not_allowed");
        Assert.Equal(feature.Id, await LinksTestData.StoredParentAsync(factory, story));
    }

    // US-2 · "Same level and skipped levels are allowed" (doc-002 W-1).
    [Fact]
    public async Task Put_SameLevelAndSkippedLevels_Return200()
    {
        var (world, items) = await WorldAsync();

        await LinksTestData.LinkAsync(factory, world, items["MSK-BUG-4"], items["MSK-US-2"]);
        await LinksTestData.LinkAsync(factory, world, items["MSK-TASK-3"], items["MSK-EPIC-1"]);

        Assert.Equal(items["MSK-US-2"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-BUG-4"]));
        Assert.Equal(items["MSK-EPIC-1"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-TASK-3"]));
    }

    // US-2 · "An item is not its own parent".
    [Fact]
    public async Task Put_Itself_Returns409SelfParent()
    {
        var (world, items) = await WorldAsync();
        var feature = items["MSK-FEAT-6"];

        var answer = await LinksTestData.SetParentAsync(factory, world, feature.Identifier, feature.Id, null);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.self_parent");
        Assert.Equal("Рабочий элемент не может быть родителем самого себя.", answer.Detail);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, feature));
    }

    // US-2 · "A cycle is refused, naming its path": MSK-TASK-12 as the parent of MSK-TASK-10, where 10 → 11 → 12.
    [Fact]
    public async Task Put_ADescendant_Returns409Cycle_WithItsPath_EveryLinkStays()
    {
        var (world, items) = await WorldAsync(upTo: 12);
        var (task10, task11, task12) = (items["MSK-TASK-10"], items["MSK-TASK-11"], items["MSK-TASK-12"]);
        await LinksTestData.LinkAsync(factory, world, task11, task10);
        await LinksTestData.LinkAsync(factory, world, task12, task11);

        var answer = await LinksTestData.SetParentAsync(factory, world, task10.Identifier, task12.Id, null);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.cycle");
        Assert.Equal("Нельзя выбрать родителем MSK-TASK-12: он вложен в MSK-TASK-10 (MSK-TASK-10 → MSK-TASK-11 → MSK-TASK-12).", answer.Detail);
        var parameters = answer.Body.GetProperty("params");
        Assert.Equal(("MSK-TASK-12", "MSK-TASK-10", "MSK-TASK-10 → MSK-TASK-11 → MSK-TASK-12"), (
            parameters.GetProperty("parent").GetProperty("value").GetString(),
            parameters.GetProperty("item").GetProperty("value").GetString(),
            parameters.GetProperty("path").GetProperty("value").GetString()));
        Assert.Equal(((Guid?)null, (Guid?)task10.Id, (Guid?)task11.Id), (
            await LinksTestData.StoredParentAsync(factory, task10),
            await LinksTestData.StoredParentAsync(factory, task11),
            await LinksTestData.StoredParentAsync(factory, task12)));
    }

    // US-2 · "An item of another project is refused as not found" (BR-H-1): «Склад»'s WH-EPIC-1 by its id, another
    // organization's item, an unknown id and a malformed one all answer alike, and nothing changes.
    [Fact]
    public async Task Put_AParentOutsideTheProject_Returns404ParentNotFound_TheSameForEveryCase()
    {
        var (world, items) = await WorldAsync();
        var feature = items["MSK-FEAT-6"];
        var warehouse = await WorkItemsTestData.AddProjectAsync(factory, world, "WH", "Склад");
        var whEpic = await WorkItemsTestData.CreateAsync(factory, warehouse, "EPIC", "Склад: учёт");
        var otherOrganization = await WorkItemsTestData.CreateWorldAsync(factory);
        var foreignEpic = await WorkItemsTestData.CreateAsync(factory, otherOrganization, "EPIC", "Чужой эпик");

        foreach (var parentId in new[] { whEpic.Body.GetProperty("id").GetString(), foreignEpic.Body.GetProperty("id").GetString(), Guid.CreateVersion7().ToString(), "abc" })
        {
            var answer = await OrganizationsApi.SendAsync(
                factory, HttpMethod.Put, LinksTestData.Parent(world, feature.Identifier), new { parentId }, OrganizationsTokens.SignedIn());

            LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "workitems.link.parent_not_found");
            Assert.Equal("Родитель не найден в этом проекте.", answer.Detail);
        }

        Assert.Null(await LinksTestData.StoredParentAsync(factory, feature));
    }

    // US-2 · "A parent chosen from a stale view is refused" and "Two users map the same child at once, the later is
    // refused" (BR-H-4): Борис's write saw no parent where Анна's stored one; a malformed seen id never matches.
    [Fact]
    public async Task Put_FromAStaleView_Returns409ParentChanged_TheStoredParentStays()
    {
        var (world, items) = await WorldAsync();
        var (story, anna, boris) = (items["MSK-US-2"], items["MSK-FEAT-6"], items["MSK-FEAT-8"]);

        var first = await LinksTestData.SetParentAsync(factory, world, story.Identifier, anna.Id, null);
        var later = await LinksTestData.SetParentAsync(factory, world, story.Identifier, boris.Id, null);
        var clearing = await LinksTestData.SetParentAsync(factory, world, story.Identifier, null, boris.Id);
        var malformed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, LinksTestData.Parent(world, story.Identifier), new { parentId = (string?)null, seenParentId = "abc" }, OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, first.Status);
        LinksTestData.AssertRefused(later, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        Assert.Equal(ParentChangedText, later.Detail);
        LinksTestData.AssertRefused(clearing, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        LinksTestData.AssertRefused(malformed, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        Assert.Equal(anna.Id, await LinksTestData.StoredParentAsync(factory, story));
    }

    // US-2 · "Two users map the same child at once, the later is refused", under a real overlap: Анна's write holds the
    // project's hierarchy at its commit; Борис's, made from the same view, waits on it, then reads Анна's parent and is
    // refused. One parent, Анна's.
    [Fact]
    public async Task Put_TwoWritesFromOneViewAtOnce_TheSecondWaits_ThenIsRefusedAsParentChanged()
    {
        var (world, items) = await WorldAsync();
        var (story, anna, boris) = (items["MSK-US-2"], items["MSK-FEAT-6"], items["MSK-FEAT-8"]);

        using var gate = factory.CommitGates.Open();
        var first = LinksTestData.SetParentAsync(factory, world, story.Identifier, anna.Id, null, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "Анна's write finished before reaching its commit gate.");

        var second = LinksTestData.SetParentAsync(factory, world, story.Identifier, boris.Id, null);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "Борис's write did not wait on the project's hierarchy.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.OK, a.Status);
        LinksTestData.AssertRefused(b, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        Assert.Equal(anna.Id, await LinksTestData.StoredParentAsync(factory, story));
    }

    // US-2 · "Two writes that would close a cycle never both succeed": MSK-TASK-3 as the parent of MSK-TASK-10 and, at
    // the same moment, MSK-TASK-12 as the parent of MSK-TASK-3. The second waits on the hierarchy, then walks the first's
    // link and is refused with the cycle's path; no item is its own ancestor.
    [Fact]
    public async Task Put_TwoWritesClosingACycleAtOnce_TheSecondIsRefusedAsCycle()
    {
        var (world, items) = await WorldAsync(upTo: 12);
        var (task3, task10, task11, task12) = (items["MSK-TASK-3"], items["MSK-TASK-10"], items["MSK-TASK-11"], items["MSK-TASK-12"]);
        await LinksTestData.LinkAsync(factory, world, task11, task10);
        await LinksTestData.LinkAsync(factory, world, task12, task11);

        using var gate = factory.CommitGates.Open();
        var first = LinksTestData.SetParentAsync(factory, world, task10.Identifier, task3.Id, null, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first write finished before reaching its commit gate.");

        var second = LinksTestData.SetParentAsync(factory, world, task3.Identifier, task12.Id, null);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second write did not wait on the project's hierarchy.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.OK, a.Status);
        LinksTestData.AssertRefused(b, HttpStatusCode.Conflict, "workitems.link.cycle");
        Assert.Equal("Нельзя выбрать родителем MSK-TASK-12: он вложен в MSK-TASK-3 (MSK-TASK-3 → MSK-TASK-10 → MSK-TASK-11 → MSK-TASK-12).", b.Detail);
        Assert.Equal(((Guid?)task3.Id, (Guid?)null), (await LinksTestData.StoredParentAsync(factory, task10), await LinksTestData.StoredParentAsync(factory, task3)));
    }

    // US-1 · "A save of other fields leaves the parent alone", under a real overlap: a form save of the item's title waits
    // on the parent write's lock on the item, then keeps the parent it wrote; both take effect.
    [Fact]
    public async Task Put_AndAFormSaveOfTheSameItemAtOnce_BothTakeEffect()
    {
        var (world, items) = await WorldAsync();
        var (story, feature) = (items["MSK-US-2"], items["MSK-FEAT-6"]);

        using var gate = factory.CommitGates.Open();
        var link = LinksTestData.SetParentAsync(factory, world, story.Identifier, feature.Id, null, gate);
        await Task.WhenAny(gate.Reached, link);
        Assert.False(link.IsCompleted, "The parent write finished before reaching its commit gate.");

        var save = WorkItemsTestData.SaveAsync(factory, world, story.Identifier, new { title = "Письмо со ссылкой на вход" });
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, save);
        Assert.False(save.IsCompleted, "The save did not wait on the parent write's lock on the item.");

        gate.Release();
        var (linked, saved) = (await link, await save);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (linked.Status, saved.Status));
        Assert.Equal(("Письмо со ссылкой на вход", "MSK-FEAT-6"), (
            saved.Body.GetProperty("title").GetString(), LinksTestData.IdentifierOf(saved.Body.GetProperty("parent"))));
    }

    // --- US-7 -------------------------------------------------------------------------------------------------------

    // US-7 · "No link is written to a deleted parent": MSK-EPIC-20 in «Удалена»; MSK-FEAT-23 keeps MSK-FEAT-8.
    [Fact]
    public async Task Put_ADeletedParent_Returns409ItemDeleted_TheOldLinkStays()
    {
        var (world, items) = await WorldAsync(upTo: 23);
        var (feature23, feature8, epic20) = (items["MSK-FEAT-23"], items["MSK-FEAT-8"], items["MSK-EPIC-20"]);
        await LinksTestData.LinkAsync(factory, world, feature23, feature8);
        await LinksTestData.DeleteAsync(factory, world, epic20);

        var answer = await LinksTestData.SetParentAsync(factory, world, feature23.Identifier, epic20.Id, feature8.Id);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.item_deleted");
        Assert.Equal("Удалённый рабочий элемент нельзя связывать.", answer.Detail);
        Assert.Equal(feature8.Id, await LinksTestData.StoredParentAsync(factory, feature23));
    }

    // US-7 · "A deleted child takes no link write": set, replace or clear; its link to MSK-FEAT-8 stays.
    [Fact]
    public async Task Put_ADeletedChild_Returns409ItemDeleted_WhateverTheWrite()
    {
        var (world, items) = await WorldAsync(upTo: 23);
        var (feature23, feature8, epic1) = (items["MSK-FEAT-23"], items["MSK-FEAT-8"], items["MSK-EPIC-1"]);
        await LinksTestData.LinkAsync(factory, world, feature23, feature8);
        await LinksTestData.DeleteAsync(factory, world, feature23);

        foreach (var parentId in new Guid?[] { epic1.Id, null, feature8.Id })
        {
            LinksTestData.AssertRefused(
                await LinksTestData.SetParentAsync(factory, world, feature23.Identifier, parentId, feature8.Id),
                HttpStatusCode.Conflict,
                "workitems.link.item_deleted");
        }

        Assert.Equal(feature8.Id, await LinksTestData.StoredParentAsync(factory, feature23));
    }

    // US-7 · "Deleting a parent keeps its children's links": the children keep MSK-EPIC-1 as their stored parent and
    // their states. US-7 · "The mapping replaces the link to a deleted parent" (BR-H-4): a write that saw «Нет» is
    // accepted although MSK-EPIC-1 is stored, and the deleted epic is left exactly as it was.
    [Fact]
    public async Task Put_ChildOfADeletedParent_KeepsTheLink_UntilAWriteThatSawNoneReplacesIt()
    {
        var (world, items) = await WorldAsync(upTo: 20);
        var (epic1, feature6, feature8, epic20) = (items["MSK-EPIC-1"], items["MSK-FEAT-6"], items["MSK-FEAT-8"], items["MSK-EPIC-20"]);
        await LinksTestData.LinkAsync(factory, world, feature6, epic1);
        await LinksTestData.LinkAsync(factory, world, feature8, epic1);
        var stateBefore = (await LinksTestData.StoredAsync(factory, feature6)).StateId;
        await LinksTestData.DeleteAsync(factory, world, epic1);
        var deletedEpic = await LinksTestData.StoredAsync(factory, epic1);

        Assert.Equal((epic1.Id, epic1.Id), (await LinksTestData.StoredParentAsync(factory, feature6), await LinksTestData.StoredParentAsync(factory, feature8)));
        Assert.Equal(stateBefore, (await LinksTestData.StoredAsync(factory, feature6)).StateId);

        var mapped = await LinksTestData.SetParentAsync(factory, world, feature6.Identifier, epic20.Id, null);
        var cleared = await LinksTestData.SetParentAsync(factory, world, feature8.Identifier, null, null);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (mapped.Status, cleared.Status));
        Assert.Equal("MSK-EPIC-20", LinksTestData.IdentifierOf(mapped.Body.GetProperty("parent")));
        Assert.Equal(((Guid?)epic20.Id, (Guid?)null), (await LinksTestData.StoredParentAsync(factory, feature6), await LinksTestData.StoredParentAsync(factory, feature8)));
        Assert.Equal(deletedEpic, await LinksTestData.StoredAsync(factory, epic1));
    }

    // --- The address, access and IDOR (api-contract § 0) ------------------------------------------------------------

    // The item resolves as the read's: another project's identifier (WH-EPIC-1 under «Мастаск»), a type key that
    // differs, an unknown number — «Рабочий элемент не найден», before the parent is looked at.
    [Theory]
    [InlineData("WH-EPIC-1")]
    [InlineData("MSK-BUG-2")]
    [InlineData("MSK-US-99")]
    [InlineData("msk-us-2")]
    public async Task Put_IdentifierResolvingToNothing_Returns404WorkItemNotFound(string identifier)
    {
        var (world, _) = await WorldAsync(upTo: 2);
        var warehouse = await WorkItemsTestData.AddProjectAsync(factory, world, "WH", "Склад");
        await WorkItemsTestData.CreateAsync(factory, warehouse, "EPIC", "Склад: учёт");

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, LinksTestData.Parent(world, identifier), new { parentId = "abc" }, OrganizationsTokens.SignedIn());

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // Another organization's project under this organization's address is «Проект не найден»: no item of another
    // organization is ever reached, as child or parent.
    [Fact]
    public async Task Put_AnotherOrganizationsProject_Returns404ProjectNotFound_NothingChanges()
    {
        var (world, items) = await WorldAsync(upTo: 6);
        var other = await WorkItemsTestData.CreateWorldAsync(factory);
        var foreignItems = await LinksTestData.CreateItemsAsync(factory, other, upTo: 2);
        var crossed = world with { ProjectId = other.ProjectId };

        var answer = await LinksTestData.SetParentAsync(factory, crossed, "MSK-US-2", items["MSK-FEAT-6"].Id, null);

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Null(await LinksTestData.StoredParentAsync(factory, foreignItems["MSK-US-2"]));
    }

    // spec § Access: an anonymous caller gets 401, whatever the body.
    [Fact]
    public async Task Put_NoToken_Returns401()
    {
        var (world, items) = await WorldAsync(upTo: 6);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, LinksTestData.Parent(world, "MSK-US-2"), new { parentId = items["MSK-FEAT-6"].Id.ToString() });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, items["MSK-US-2"]));
    }
}
