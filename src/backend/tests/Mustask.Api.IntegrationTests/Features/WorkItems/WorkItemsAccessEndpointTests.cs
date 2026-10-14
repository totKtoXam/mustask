using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-016 US-2 · "A member writes work items, a reader reads them" (ADR-0027, api-contract § 4.2 rows 28–37): a member holds
/// <c>workitems.workitem.write</c>, <c>workitems.link.write</c> and <c>boards.backlog.reorder</c> on top of the reads, a viewer only
/// the reads. Both open a project's work items, a team's backlog and its board; the member creates a work item, changes its
/// fields and its parent and reorders the backlog, each kept after a reload; the viewer's same requests answer «Недостаточно
/// прав» and change nothing. Every request below is a real HTTP call through the scoped check.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemsAccessEndpointTests(MustaskApiFactory factory)
{
    private const string ForbiddenCode = "organizations.access.forbidden";

    private async Task<(BoardsWorld World, MembershipTestData.TestMember Member, MembershipTestData.TestMember Viewer)> WorldAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        return (world, member, viewer);
    }

    private static string Reads(BoardsWorld world, Guid teamId, string identifier) => string.Join(
        '|',
        WorkItemsTestData.List(world.Items),
        WorkItemsTestData.Item(world.Items, identifier),
        BoardsTestData.Backlog(world, teamId, "stories"),
        BoardsTestData.Board(world, teamId, "stories"));

    // US-2 · both roles open a project's work items, a work item, a team's backlog and its board (rows 28, 30, 33, 35, 37), and
    // the process of the organization (row 27).
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task MemberAndViewer_BothRead_TheWorkItemsTheBacklogAndTheBoard(OrganizationRole role)
    {
        var (world, member, viewer) = await WorldAsync();
        var item = await BoardsTestData.CreateAsync(factory, world, "US", "Читаемая", world.PlatformAreaId);
        var token = role == OrganizationRole.Member ? member.Token : viewer.Token;

        foreach (var path in Reads(world, world.PlatformId, item.Identifier).Split('|')
            .Append($"{WorkItemsTestData.Item(world.Items, item.Identifier)}/parent-candidates")
            .Append($"/api/organizations/{world.OrganizationId}/process"))
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: token);

            Assert.True(answer.Status == HttpStatusCode.OK, $"GET {path} answered {(int)answer.Status} {answer.ErrorCode} to a {role}.");
        }
    }

    // US-2 · "the member creates a work item, changes its fields and its parent, reorders the backlog and moves a card, each kept
    // after a reload" (rows 29, 31, 32, 36): a created item with a parent, a saved title and state, a parent set by the parent
    // write and cleared by a save, a reorder — and the reload shows them all.
    [Fact]
    public async Task Member_CreatesAnItemWithAParent_SavesItsFields_SetsItsParent_ReordersTheBacklog_EachKeptAfterAReload()
    {
        var (world, member, _) = await WorldAsync();
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Родительская история", world.PlatformAreaId);
        var other = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая история", world.PlatformAreaId);

        // Row 29: a create with a parentId needs the second code too, which a member holds.
        var created = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types["TASK"].Id.ToString(),
                title = "Задача участника",
                parentId = story.Id.ToString(),
            },
            member.Token);
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var task = created.Body.GetProperty("identifier").GetString()!;
        Assert.Equal(story.Id, created.Body.GetProperty("parent").GetProperty("id").GetGuid());

        // Row 31: a save of the fields (and a state move, which is a save on the same route).
        var inProgress = world.Items.Types["TASK"].StateId("Активна");
        var saved = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Patch,
            WorkItemsTestData.Item(world.Items, task),
            new { title = "Задача участника, уточнённая", stateId = inProgress.ToString() },
            member.Token);
        Assert.Equal(HttpStatusCode.OK, saved.Status);

        // Row 32: the parent write replaces the parent.
        var reparented = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{WorkItemsTestData.Item(world.Items, task)}/parent",
            new { parentId = other.Id.ToString(), seenParentId = story.Id.ToString() },
            member.Token);
        Assert.Equal(HttpStatusCode.OK, reparented.Status);

        // Row 36: the reorder.
        var before = await BoardsTestData.RowsAsync(factory, world, world.PlatformId);
        var reorder = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{BoardsTestData.Backlog(world, world.PlatformId, "stories")}/reorder",
            BoardsTestData.Top(other),
            member.Token);
        Assert.Equal(HttpStatusCode.NoContent, reorder.Status);

        // After a reload: everything is kept.
        var reload = await WorkItemsTestData.ReadAsync(factory, world.Items, task);
        Assert.Equal("Задача участника, уточнённая", reload.GetProperty("title").GetString());
        Assert.Equal(inProgress, reload.GetProperty("state").GetProperty("id").GetGuid());
        Assert.Equal(other.Id, reload.GetProperty("parent").GetProperty("id").GetGuid());
        var after = await BoardsTestData.RowsAsync(factory, world, world.PlatformId);
        Assert.Equal(other.Identifier, after[0]);
        Assert.NotEqual(before, after);
    }

    // US-2 · "the viewer's same requests answer «Недостаточно прав»": a create (with and without a parent), a save, a parent write,
    // a reorder and the assignee picker are all refused with 403 and nothing changes (rows 29, 31, 32, 34, 36).
    [Fact]
    public async Task Viewer_IsRefusedEveryWrite_With403_AndNothingChanges()
    {
        var (world, _, viewer) = await WorldAsync();
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Историй", world.PlatformAreaId);
        var other = await BoardsTestData.CreateAsync(factory, world, "US", "Другая", world.PlatformAreaId);
        var storedBefore = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var rowsBefore = await BoardsTestData.RowsAsync(factory, world, world.PlatformId);
        var typeId = world.Items.Types["TASK"].Id.ToString();

        var attempts = new (string Name, HttpMethod Method, string Path, object? Body)[]
        {
            ("create", HttpMethod.Post, WorkItemsTestData.List(world.Items), new { requestId = Guid.NewGuid().ToString(), typeId, title = "Нельзя" }),
            (
                "create with a parent",
                HttpMethod.Post,
                WorkItemsTestData.List(world.Items),
                new { requestId = Guid.NewGuid().ToString(), typeId, title = "Нельзя", parentId = story.Id.ToString() }),
            ("save", HttpMethod.Patch, WorkItemsTestData.Item(world.Items, story.Identifier), new { title = "Взломано" }),
            (
                "save with a parent",
                HttpMethod.Patch,
                WorkItemsTestData.Item(world.Items, story.Identifier),
                new { parent = new { parentId = other.Id.ToString() } }),
            ("parent write", HttpMethod.Put, $"{WorkItemsTestData.Item(world.Items, story.Identifier)}/parent", new { parentId = other.Id.ToString() }),
            (
                "reorder",
                HttpMethod.Post,
                $"{BoardsTestData.Backlog(world, world.PlatformId, "stories")}/reorder",
                BoardsTestData.Top(other)),
            ("assignee candidates", HttpMethod.Get, WorkItemsTestData.Candidates(world.OrganizationId, "аб"), null),
        };

        foreach (var (name, method, path, body) in attempts)
        {
            var answer = await OrganizationsApi.SendAsync(factory, method, path, body, viewer.Token);

            Assert.True(
                answer.Status == HttpStatusCode.Forbidden && answer.ErrorCode == ForbiddenCode,
                $"A viewer's {name} answered {(int)answer.Status} {answer.ErrorCode}.");
            Assert.Equal("Недостаточно прав", answer.Detail);
        }

        Assert.Equal(storedBefore, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        Assert.Equal(rowsBefore, await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-2 · the member holds the assignee picker (a GET with a write code, row 34) — and still gets only the answer the picker gives.
    [Fact]
    public async Task Member_OpensTheAssigneePicker()
    {
        var (world, member, _) = await WorldAsync();

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, WorkItemsTestData.Candidates(world.OrganizationId, "аб"), token: member.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    // US-2 / doc-006 US-4 scenario 2 · "the resource's organization decides": a user who is admin of A and viewer of B writes work
    // items in A and is refused in B, with the same token — the role is read per organization, not per user.
    [Fact]
    public async Task AdminOfA_AndViewerOfB_WritesInA_IsRefusedInB_WithTheSameToken()
    {
        var a = await WorkItemsTestData.CreateWorldAsync(factory);
        var b = await WorkItemsTestData.CreateWorldAsync(factory);
        var user = await MembershipTestData.NewMemberAsync(factory, a.OrganizationId, OrganizationRole.Admin);
        await MembershipTestData.AddMemberAsync(factory, b.OrganizationId, user.Id, OrganizationRole.Viewer);

        var inA = await WorkItemsTestData.CreateAsync(factory, a, "TASK", "В организации A", token: user.Token);
        var inB = await WorkItemsTestData.CreateAsync(factory, b, "TASK", "В организации B", token: user.Token);
        var readInB = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.List(b), token: user.Token);

        Assert.Equal(HttpStatusCode.Created, inA.Status);
        Assert.Equal((HttpStatusCode.Forbidden, ForbiddenCode), (inB.Status, inB.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, readInB.Status);
        Assert.Empty(await WorkItemsTestData.StoredAsync(factory, b.ProjectId));
    }
}
