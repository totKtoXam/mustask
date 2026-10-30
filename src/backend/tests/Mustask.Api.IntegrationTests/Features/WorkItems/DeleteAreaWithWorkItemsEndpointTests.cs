using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-002's <c>DELETE …/areas/{areaId}</c> with work items in the tree
/// (mod-003 US-6, BR-M3-24, api-contract § 6): an area that holds an item, in
/// any state, is refused with mod-002's own <c>organizations.area.not_empty</c>,
/// and a deletion racing a save into the area never lets both succeed.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteAreaWithWorkItemsEndpointTests(MustaskApiFactory factory)
{
    private const string NotEmptyText = "Нельзя удалить область, в которой есть проекты, вложенные области или рабочие элементы";

    /// <summary>«Мастаск» with the leaves «Отчёты» and «Платформа» under its root, and MSK-TASK-1 in the root.</summary>
    private async Task<(WorkItemsWorld World, Guid Reports, Guid Platform, string Task)> WorldAsync()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.RootAreaId, "Отчёты");
        var platform = await OrganizationsTestData.AddAreaAsync(factory, world.ProjectId, world.RootAreaId, "Платформа");
        var task = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Задача");

        return (world, reports.Id, platform.Id, task);
    }

    private Task<Answer> DeleteAsync(WorkItemsWorld world, Guid areaId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Areas(world.OrganizationId, world.ProjectId)}/{areaId}",
            token: OrganizationsTokens.SignedIn(),
            gate: gate);

    private async Task<bool> AreaExistsAsync(WorkItemsWorld world, Guid areaId) =>
        (await OrganizationsTestData.ReadTreeAsync(factory, world.ProjectId)).Contains(areaId);

    // US-6 · "An area holding a work item is not deleted": mod-002's dialog text, the area stays and the item keeps it.
    [Fact]
    public async Task Delete_AreaHoldingAWorkItem_Returns409NotEmpty_AreaAndItemUnchanged()
    {
        var (world, reports, _, task) = await WorldAsync();
        await WorkItemsTestData.SaveAsync(factory, world, task, new { areaId = reports.ToString() });

        var answer = await DeleteAsync(world, reports);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.not_empty", answer.ErrorCode);
        Assert.Equal(NotEmptyText, answer.Detail);
        Assert.True(await AreaExistsAsync(world, reports));
        Assert.Equal(reports, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0].AreaId);
    }

    // US-6 · "Every item counts, whatever its state": the only item in «Отчёты» is in «Закрыта».
    [Fact]
    public async Task Delete_AreaWhoseOnlyItemIsClosed_IsRefusedAsWell()
    {
        var (world, reports, _, task) = await WorldAsync();
        await WorkItemsTestData.SaveAsync(factory, world, task, new
        {
            areaId = reports.ToString(),
            stateId = world.Types["TASK"].StateId(StateCategory.Completed).ToString(),
        });

        var answer = await DeleteAsync(world, reports);

        Assert.Equal("organizations.area.not_empty", answer.ErrorCode);
        Assert.True(await AreaExistsAsync(world, reports));
    }

    // US-6 · "The area is deleted once its items are moved out".
    [Fact]
    public async Task Delete_AfterTheItemMovedOut_Returns204()
    {
        var (world, reports, platform, task) = await WorldAsync();
        await WorkItemsTestData.SaveAsync(factory, world, task, new { areaId = reports.ToString() });
        await WorkItemsTestData.SaveAsync(factory, world, task, new { areaId = platform.ToString() });

        var answer = await DeleteAsync(world, reports);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.False(await AreaExistsAsync(world, reports));
    }

    // US-6 · "Items of other areas do not block a deletion".
    [Fact]
    public async Task Delete_ItemsInOtherAreasOnly_Returns204()
    {
        var (world, reports, platform, task) = await WorldAsync();
        await WorkItemsTestData.SaveAsync(factory, world, task, new { areaId = platform.ToString() });
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Баг");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { areaId = platform.ToString() });

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(world, reports)).Status);
    }

    // US-6 · "A deletion and an item entering the area at the same moment", in either order (BR-M3-24): the request that
    // goes first is held at its commit with its lock on «Отчёты» — the deletion FOR UPDATE, the save FOR SHARE — and the
    // other is shown to wait on it. Deletion first: the save answers «Область не найдена.» and MSK-TASK-1 stays in
    // «Платформа». Save first: the deletion answers not_empty and «Отчёты» stays with the item in it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ConcurrentWithASaveIntoTheArea_ExactlyOneSucceeds_BothOrders(bool deletionFirst)
    {
        var (world, reports, platform, task) = await WorldAsync();
        await WorkItemsTestData.SaveAsync(factory, world, task, new { areaId = platform.ToString() });
        Func<CommitGate?, Task<Answer>> delete = gate => DeleteAsync(world, reports, gate);
        Func<CommitGate?, Task<Answer>> save = gate => WorkItemsTestData.SaveAsync(factory, world, task, new { areaId = reports.ToString() }, gate);

        var (deleted, saved) = deletionFirst
            ? await RaceAsync(delete, save)
            : Swap(await RaceAsync(save, delete));

        var item = (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0];
        Assert.True(await AreaExistsAsync(world, item.AreaId), "An item points at an area that no longer exists.");

        if (deletionFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
            Assert.Equal(HttpStatusCode.NotFound, saved.Status);
            Assert.Equal(("organizations.area.not_found", "Область не найдена."), (saved.ErrorCode, saved.Detail));
            Assert.Equal(platform, item.AreaId);
            Assert.False(await AreaExistsAsync(world, reports));
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, saved.Status);
            Assert.Equal(HttpStatusCode.Conflict, deleted.Status);
            Assert.Equal("organizations.area.not_empty", deleted.ErrorCode);
            Assert.Equal(reports, item.AreaId);
            Assert.True(await AreaExistsAsync(world, reports));
        }
    }

    /// <summary>Request A is held at its commit with its locks taken, B is shown to wait on one of them, then A is released.</summary>
    private async Task<(Answer First, Answer Second)> RaceAsync(Func<CommitGate?, Task<Answer>> first, Func<CommitGate?, Task<Answer>> second)
    {
        using var gate = factory.CommitGates.Open();

        var holder = first(gate);
        await Task.WhenAny(gate.Reached, holder);
        Assert.False(holder.IsCompleted, "The first request finished before reaching its commit gate.");

        var waiter = second(null);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, waiter);
        Assert.False(waiter.IsCompleted, "The second request did not wait on the first one's lock on the area.");

        gate.Release();

        return (await holder, await waiter);
    }

    private static (Answer Deletion, Answer Save) Swap((Answer First, Answer Second) answers) => (answers.Second, answers.First);
}
