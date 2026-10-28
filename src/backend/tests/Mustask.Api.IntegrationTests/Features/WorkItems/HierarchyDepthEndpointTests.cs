using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// The hierarchy's depth bound (feat-027 D-9, review B-1/B-2): a chain of parents holds at most 64 items, from a top
/// item down to its deepest descendant. The three writes that make a link — feat-001's
/// <c>PUT …/workitems/{identifier}/parent</c>, the form's <c>PATCH</c> with a <c>parent</c> and the create's
/// <c>parentId</c> (feat-027 api-contract § 3) — accept a link that makes a chain of 64 and refuse one that makes 65
/// with 409 <c>workitems.link.depth_exceeded</c>, after the cycle check and the other link checks. The chains are
/// tasks, a task being allowed beneath a task (BR-H-2), put straight into the table: the endpoints would take 64 calls a
/// chain.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class HierarchyDepthEndpointTests(MustaskApiFactory factory)
{
    private const string Refusal = "Слишком глубокая вложенность: в цепочке родителей может быть не больше 64 элементов.";

    private async Task<(WorkItemsWorld World, Item[] Chain)> WorldWithChainAsync(int length)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        return (world, await BulkItemsTestData.InsertChainAsync(factory, world, world.RootAreaId, length));
    }

    private async Task<Item> NewTaskAsync(WorkItemsWorld world, string title)
    {
        var answer = await WorkItemsTestData.CreateAsync(factory, world, "TASK", title);
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    /// <summary>The create request as a row's «Добавить: <Тип>» sends it (feat-027 api-contract § 3): the parent, no team.</summary>
    private Task<Answer> CreateChildAsync(WorkItemsWorld world, string typeKey, string title, Guid parentId) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Types[typeKey].Id.ToString(), title, parentId = parentId.ToString() },
            OrganizationsTokens.SignedIn());

    private static object Parent(Item? parent, Item? seen) => new { parentId = parent?.Id.ToString(), seenParentId = seen?.Id.ToString() };

    private static void AssertDepthRefused(Answer answer)
    {
        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.depth_exceeded");
        Assert.Equal(Refusal, answer.Detail);
        Assert.Equal(64, answer.Body.GetProperty("params").GetProperty("limit").GetProperty("value").GetInt32());
    }

    // --- The parent write (feat-001 § 3.2) --------------------------------------------------------------------------

    // D-9 · a task beneath the 63rd link of a chain makes a chain of 64 and is accepted; one more beneath it would make 65
    // and is refused — «Слишком глубокая вложенность…» with the limit as a parameter — and nothing changes.
    [Fact]
    public async Task Put_ALinkMakingAChainOf64_Accepted_OneMakingAChainOf65_Refused409DepthExceeded()
    {
        var (world, chain) = await WorldWithChainAsync(63);
        var last = await NewTaskAsync(world, "Шестьдесят четвёртое");
        var over = await NewTaskAsync(world, "Шестьдесят пятое");

        var accepted = await LinksTestData.SetParentAsync(factory, world, last.Identifier, chain[^1].Id, null);
        var overBefore = await LinksTestData.StoredAsync(factory, over);
        var refused = await LinksTestData.SetParentAsync(factory, world, over.Identifier, last.Id, null);

        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(chain[^1].Identifier, LinksTestData.IdentifierOf(accepted.Body.GetProperty("parent")));
        AssertDepthRefused(refused);
        Assert.Equal(overBefore, await LinksTestData.StoredAsync(factory, over));
        Assert.Equal(chain[^1].Id, await LinksTestData.StoredParentAsync(factory, last));
    }

    // D-9 · the moved item's own subtree counts, not the item alone: a chain of 24 beneath the tail of a chain of 40
    // makes 64 and is accepted; a chain of 25 would make 65 and is refused, its link unchanged.
    [Fact]
    public async Task Put_AMovedSubtree_CountsItsHeight_24BeneathA40Chain_Accepted_25Refused()
    {
        var (world, parentChain) = await WorldWithChainAsync(40);
        var fits = await BulkItemsTestData.InsertChainAsync(factory, world, world.RootAreaId, 24, firstNumber: 41);
        var tooTall = await BulkItemsTestData.InsertChainAsync(factory, world, world.RootAreaId, 25, firstNumber: 65);

        var refused = await LinksTestData.SetParentAsync(factory, world, tooTall[0].Identifier, parentChain[^1].Id, null);
        var accepted = await LinksTestData.SetParentAsync(factory, world, fits[0].Identifier, parentChain[^1].Id, null);

        AssertDepthRefused(refused);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, tooTall[0]));
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(parentChain[^1].Id, await LinksTestData.StoredParentAsync(factory, fits[0]));
    }

    // D-9 · taking an item out of its parent is never a depth check, whatever it holds beneath it: a clear is accepted
    // from the middle of a full chain.
    [Fact]
    public async Task Put_ClearingTheParentOfAFullChainsSubtree_IsNotRefused()
    {
        var (world, chain) = await WorldWithChainAsync(64);

        var cleared = await LinksTestData.SetParentAsync(factory, world, chain[1].Identifier, null, chain[0].Id);

        Assert.Equal(HttpStatusCode.OK, cleared.Status);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, chain[1]));
    }

    // D-9 · the order of the parent write's answers: the stale view, the level and the cycle are each answered before the
    // depth, although the chain is full and the depth would refuse too.
    [Fact]
    public async Task Put_AFullChain_TheOtherChecksAnswerBeforeTheDepth()
    {
        var (world, chain) = await WorldWithChainAsync(64);
        var loose = await NewTaskAsync(world, "Свободная");
        var story = await WorkItemsTestData.CreateAsync(factory, world, "US", "История");
        Assert.Equal(HttpStatusCode.Created, story.Status);
        var storyIdentifier = story.Body.GetProperty("identifier").GetString()!;

        // The chain's top beneath its own tail: a cycle, although it would be too deep as well.
        var cycle = await LinksTestData.SetParentAsync(factory, world, chain[0].Identifier, chain[^1].Id, null);
        // A story beneath the tail: a level the process does not allow.
        var level = await LinksTestData.SetParentAsync(factory, world, storyIdentifier, chain[^1].Id, null);
        // A view that saw the item with a parent it has not: stale.
        var stale = await LinksTestData.SetParentAsync(factory, world, loose.Identifier, chain[^1].Id, chain[3].Id);
        var depth = await LinksTestData.SetParentAsync(factory, world, loose.Identifier, chain[^1].Id, null);

        LinksTestData.AssertRefused(cycle, HttpStatusCode.Conflict, "workitems.link.cycle");
        LinksTestData.AssertRefused(level, HttpStatusCode.Conflict, "workitems.link.parent_level_not_allowed");
        LinksTestData.AssertRefused(stale, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        AssertDepthRefused(depth);
    }

    // --- The form's save with a parent (feat-001 § 3.3) --------------------------------------------------------------

    // D-9 · the save's `parent` is bound alike, and all or none (BR-H-6): the title travelling with the accepted link is
    // stored, the one travelling with the refused link is not.
    [Fact]
    public async Task Patch_AParentMakingAChainOf64_Accepted_OfSixtyFive_Refused_NothingElseStored()
    {
        var (world, chain) = await WorldWithChainAsync(63);
        var last = await NewTaskAsync(world, "Шестьдесят четвёртое");
        var over = await NewTaskAsync(world, "Шестьдесят пятое");

        var accepted = await WorkItemsTestData.SaveAsync(factory, world, last.Identifier, new { title = "Звено 64", parent = Parent(chain[^1], null) });
        var refused = await WorkItemsTestData.SaveAsync(factory, world, over.Identifier, new { title = "Звено 65", parent = Parent(last, null) });

        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(("Звено 64", chain[^1].Identifier), (accepted.Body.GetProperty("title").GetString(), LinksTestData.IdentifierOf(accepted.Body.GetProperty("parent"))));
        AssertDepthRefused(refused);
        var reloaded = await WorkItemsTestData.ReadAsync(factory, world, over.Identifier);
        Assert.Equal(("Шестьдесят пятое", (string?)null), (reloaded.GetProperty("title").GetString(), LinksTestData.IdentifierOf(reloaded.GetProperty("parent"))));
    }

    // D-9 · the save moves a subtree under the same count as the parent write: 25 beneath a chain of 40 refused.
    [Fact]
    public async Task Patch_AMovedSubtree_CountsItsHeight()
    {
        var (world, parentChain) = await WorldWithChainAsync(40);
        var tooTall = await BulkItemsTestData.InsertChainAsync(factory, world, world.RootAreaId, 25, firstNumber: 41);

        var refused = await WorkItemsTestData.SaveAsync(factory, world, tooTall[0].Identifier, new { parent = Parent(parentChain[^1], null) });

        AssertDepthRefused(refused);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, tooTall[0]));
    }

    // --- The create with a parent (feat-027 api-contract § 3) -------------------------------------------------------

    // D-9 · a child created beneath the 63rd link of a chain is the 64th item and is created; one beneath that would be the
    // 65th and is refused, creating nothing and consuming no number.
    [Fact]
    public async Task Create_AChildMakingAChainOf64_Created_OneMoreRefused_NoNumberConsumed()
    {
        var (world, chain) = await WorldWithChainAsync(63);

        var made = await CreateChildAsync(world, "TASK", "Шестьдесят четвёртое", chain[^1].Id);
        Assert.Equal(HttpStatusCode.Created, made.Status);
        var refused = await CreateChildAsync(world, "TASK", "Шестьдесят пятое", made.Body.GetProperty("id").GetGuid());
        var next = await WorkItemsTestData.CreateAsync(factory, world, "TASK", "Следующая");

        Assert.Equal(("MSK-TASK-64", chain[^1].Identifier), (made.Body.GetProperty("identifier").GetString(), LinksTestData.IdentifierOf(made.Body.GetProperty("parent"))));
        AssertDepthRefused(refused);
        Assert.Equal(HttpStatusCode.Created, next.Status);
        Assert.Equal("MSK-TASK-65", next.Body.GetProperty("identifier").GetString());
        Assert.Equal(65, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId)).Count);
    }

    // D-9 · the order of the create's answers (feat-027 api-contract § 3): a parent in «Удалена» and a parent of a lower
    // level than the new item are answered before the depth, with the parent at the end of a full chain.
    [Fact]
    public async Task Create_BeneathAFullChain_DeletedAndLevelAreAnsweredBeforeTheDepth()
    {
        var (world, chain) = await WorldWithChainAsync(64);

        // A story beneath a task: a parent of a lower level.
        var level = await CreateChildAsync(world, "US", "История", chain[^1].Id);
        var depth = await CreateChildAsync(world, "TASK", "Задача", chain[^1].Id);
        await LinksTestData.DeleteAsync(factory, world, chain[^1]);
        var deleted = await CreateChildAsync(world, "TASK", "Задача", chain[^1].Id);

        LinksTestData.AssertRefused(level, HttpStatusCode.Conflict, "workitems.link.parent_level_not_allowed");
        AssertDepthRefused(depth);
        LinksTestData.AssertRefused(deleted, HttpStatusCode.Conflict, "workitems.link.item_deleted");
    }
}
