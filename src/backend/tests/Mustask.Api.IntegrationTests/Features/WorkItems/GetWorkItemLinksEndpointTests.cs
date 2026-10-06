using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}</c>
/// with feat-001's <c>parent</c> and <c>children</c> (its api-contract § 2.2):
/// the form's «Родитель» — a parent in «Удалена» included, with that state — and
/// its read-only «Дочерние элементы», direct ones only, those in «Удалена»
/// excepted, by level and then by number (US-3, US-7).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetWorkItemLinksEndpointTests(MustaskApiFactory factory)
{
    /// <summary>US-3's data: MSK-EPIC-1 is the parent of MSK-FEAT-6 and MSK-TASK-3; MSK-FEAT-6 of MSK-US-2 and MSK-BUG-4.</summary>
    private async Task<(WorkItemsWorld World, IReadOnlyDictionary<string, Item> Items)> WorldAsync()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var items = await LinksTestData.CreateItemsAsync(factory, world, upTo: 8);

        await LinksTestData.LinkAsync(factory, world, items["MSK-TASK-3"], items["MSK-EPIC-1"]);
        await LinksTestData.LinkAsync(factory, world, items["MSK-FEAT-6"], items["MSK-EPIC-1"]);
        await LinksTestData.LinkAsync(factory, world, items["MSK-BUG-4"], items["MSK-FEAT-6"]);
        await LinksTestData.LinkAsync(factory, world, items["MSK-US-2"], items["MSK-FEAT-6"]);

        return (world, items);
    }

    private async Task<string[]> ChildrenAsync(WorkItemsWorld world, string identifier) =>
        LinksTestData.Identifiers((await WorkItemsTestData.ReadAsync(factory, world, identifier)).GetProperty("children"));

    // US-3 · "The children are listed by level, then by number": MSK-FEAT-6 «Вход по ссылке», then MSK-TASK-3 «Шаблон
    // письма», each with its type and its state. A story and a bug share a level: by number.
    [Fact]
    public async Task Get_Children_ByLevelThenByNumber_EachWithTypeAndState()
    {
        var (world, _) = await WorldAsync();

        var epic = await WorkItemsTestData.ReadAsync(factory, world, "MSK-EPIC-1");

        var children = epic.GetProperty("children").EnumerateArray().ToList();
        Assert.Equal(["MSK-FEAT-6", "MSK-TASK-3"], LinksTestData.Identifiers(epic.GetProperty("children")));
        Assert.Equal(("Вход по ссылке", "feature", "Новая"), (
            children[0].GetProperty("title").GetString(),
            children[0].GetProperty("type").GetProperty("appearance").GetString(),
            children[0].GetProperty("state").GetProperty("name").GetString()));
        Assert.Equal(["MSK-US-2", "MSK-BUG-4"], await ChildrenAsync(world, "MSK-FEAT-6"));
    }

    // US-3 · "Only direct children are listed": MSK-US-2 and MSK-BUG-4 are MSK-FEAT-6's, not MSK-EPIC-1's.
    [Fact]
    public async Task Get_Children_OnlyDirectOnes()
    {
        var (world, _) = await WorldAsync();

        var children = await ChildrenAsync(world, "MSK-EPIC-1");

        Assert.DoesNotContain("MSK-US-2", children);
        Assert.DoesNotContain("MSK-BUG-4", children);
    }

    // US-3 · "A child opens from the list": MSK-FEAT-6's «Родитель» reads «MSK-EPIC-1 Вход и регистрация».
    [Fact]
    public async Task Get_Parent_TheStoredParentAsAWorkItemRef()
    {
        var (world, items) = await WorldAsync();

        var parent = (await WorkItemsTestData.ReadAsync(factory, world, "MSK-FEAT-6")).GetProperty("parent");

        Assert.Equal((items["MSK-EPIC-1"].Id, "MSK-EPIC-1", "Вход и регистрация", "EPIC"), (
            parent.GetProperty("id").GetGuid(),
            parent.GetProperty("identifier").GetString(),
            parent.GetProperty("title").GetString(),
            parent.GetProperty("type").GetProperty("key").GetString()));
    }

    // US-3 · "An item with no child says so": `children` is [], and an item with no parent has `parent` null.
    [Fact]
    public async Task Get_NoChildNoParent_EmptyAndNull()
    {
        var (world, _) = await WorldAsync();

        var story = await WorkItemsTestData.ReadAsync(factory, world, "MSK-US-2");
        var feature = await WorkItemsTestData.ReadAsync(factory, world, "MSK-FEAT-8");

        Assert.Empty(story.GetProperty("children").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, feature.GetProperty("parent").ValueKind);
        Assert.Empty(feature.GetProperty("children").EnumerateArray());
    }

    // US-3 · "The list follows a link written elsewhere at the next read" (BR-H-19).
    [Fact]
    public async Task Get_ALinkWrittenElsewhere_ShowsAtTheNextRead()
    {
        var (world, items) = await WorldAsync();
        Assert.Empty(await ChildrenAsync(world, "MSK-FEAT-8"));

        await LinksTestData.LinkAsync(factory, world, items["MSK-US-2"], items["MSK-FEAT-8"]);

        Assert.Equal(["MSK-US-2"], await ChildrenAsync(world, "MSK-FEAT-8"));
        Assert.Equal(["MSK-BUG-4"], await ChildrenAsync(world, "MSK-FEAT-6"));
    }

    // US-7 · "A deleted child is not listed on its parent's form": MSK-BUG-4 in «Удалена».
    [Fact]
    public async Task Get_ADeletedChild_IsNotListed()
    {
        var (world, items) = await WorldAsync();

        await LinksTestData.DeleteAsync(factory, world, items["MSK-BUG-4"]);

        Assert.Equal(["MSK-US-2"], await ChildrenAsync(world, "MSK-FEAT-6"));
    }

    // US-7 · "The child's form lets go of a deleted parent", the read: «Родитель» reads «MSK-EPIC-1 Вход и регистрация»
    // with the state «Удалена».
    [Fact]
    public async Task Get_ADeletedParent_IsShownWithItsState()
    {
        var (world, items) = await WorldAsync();

        await LinksTestData.DeleteAsync(factory, world, items["MSK-EPIC-1"]);

        var parent = (await WorkItemsTestData.ReadAsync(factory, world, "MSK-FEAT-6")).GetProperty("parent");
        Assert.Equal(("MSK-EPIC-1", "Вход и регистрация", "Удалена", "Removed"), (
            parent.GetProperty("identifier").GetString(),
            parent.GetProperty("title").GetString(),
            parent.GetProperty("state").GetProperty("name").GetString(),
            parent.GetProperty("state").GetProperty("category").GetProperty("code").GetString()));
    }

    // mod-003 § 1.2 is unchanged: a row of the project's list carries no link fields.
    [Fact]
    public async Task List_Rows_CarryNoLinkFields()
    {
        var (world, _) = await WorldAsync();

        var row = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data")[0];

        Assert.False(row.TryGetProperty("parent", out _));
        Assert.False(row.TryGetProperty("children", out _));
    }

    // The create answers `WorkItem` too: no parent, no child.
    [Fact]
    public async Task Create_AnswersNoParentAndNoChildren()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var answer = await WorkItemsTestData.CreateAsync(factory, world, "BUG", "Новый баг");

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("parent").ValueKind);
        Assert.Empty(answer.Body.GetProperty("children").EnumerateArray());
    }
}
