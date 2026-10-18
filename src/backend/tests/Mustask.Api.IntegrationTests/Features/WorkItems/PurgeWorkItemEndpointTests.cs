using System.Collections.Concurrent;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Infrastructure;
using Mustask.Shared.Exceptions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/purge</c> (mod-005 api-contract § 2.3,
/// US-6; BR-DR-12..-17): a deleted item and everything keyed to it go in one transaction, by an administrator; the children
/// survive without it; the number is never issued again; nothing of the item is kept but one log line.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class PurgeWorkItemEndpointTests(MustaskApiFactory factory)
{
    private const string NotFoundCode = "workitems.workitem.not_found";

    private static string PurgePath(WorkItemsWorld world, string identifier) => $"{WorkItemsTestData.Item(world, identifier)}/purge";

    private Task<Answer> PurgeAsync(WorkItemsWorld world, string identifier, string? token = null, Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>? host = null) =>
        OrganizationsApi.SendAsync(host ?? factory, HttpMethod.Post, PurgePath(world, identifier), token: token ?? OrganizationsTokens.SignedIn());

    private async Task<(BoardsWorld World, Item Story)> DeletedStoryAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "На удаление", world.PlatformAreaId);
        await LinksTestData.DeleteAsync(factory, world.Items, story);

        return (world, story);
    }

    private async Task<int> RowsAsync(Guid itemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM work_items WHERE id = {itemId}")
            .SingleAsync();
    }

    // US-6 · "Confirming removes the item; 404 as if it never was; its places gone, the others keep their order": 204, then the
    // address is unknown, no row and no place remains in either order, and the other items stand where they stood.
    [Fact]
    public async Task Purge_ADeletedItem_Is204_ItsAddressIs404_NoRowNoPlaceInEitherOrder_OthersKeepTheirOrder()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.PlatformAreaId);
        var middle = await BoardsTestData.CreateAsync(factory, world, "US", "Средняя", world.PlatformAreaId);
        var last = await BoardsTestData.CreateAsync(factory, world, "US", "Последняя", world.PlatformAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, first, middle, last);
        await ProjectBoardsTestData.ReorderToAsync(factory, world, first, middle, last);
        await LinksTestData.DeleteAsync(factory, world.Items, middle);
        Assert.Equal((1, 1), (await OwningTeamTestData.TeamPlacesOfAsync(factory, middle.Id), await OwningTeamTestData.ProjectPlacesOfAsync(factory, middle.Id)));

        var answer = await PurgeAsync(world.Items, middle.Identifier);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world.Items, middle.Identifier), HttpStatusCode.NotFound, NotFoundCode);
        Assert.Equal(0, await RowsAsync(middle.Id));
        Assert.Equal((0, 0), (await OwningTeamTestData.TeamPlacesOfAsync(factory, middle.Id), await OwningTeamTestData.ProjectPlacesOfAsync(factory, middle.Id)));
        Assert.Equal([first.Identifier, last.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal([first.Identifier, last.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.Equal(
            [first.Identifier, last.Identifier],
            (await WorkItemsTestData.ListAsync(factory, world.Items)).Body.GetProperty("data").EnumerateArray()
                .Select(row => row.GetProperty("identifier").GetString()!).Order(StringComparer.Ordinal));
    }

    // US-6 · "The children of a purged item survive without it": each loses the link, keeps its state and its team, «Изменён» is
    // the purge's; a deleted child stays deleted; the purged item's own parent is untouched.
    [Fact]
    public async Task Purge_AParent_ChildrenLoseTheLink_KeepStateAndTeam_ChangedAtIsThePurge_ADeletedChildStaysDeleted_TheOwnParentIsUntouched()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик", world.PlatformAreaId);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var live = await BoardsTestData.CreateAsync(factory, world, "US", "Живая", world.PlatformAreaId);
        var gone = await BoardsTestData.CreateAsync(factory, world, "US", "Удалённая", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, feature, epic);
        await LinksTestData.LinkAsync(factory, world.Items, live, feature);
        await LinksTestData.LinkAsync(factory, world.Items, gone, feature);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, live, "US", "Активна")).Status);
        await LinksTestData.DeleteAsync(factory, world.Items, gone);
        await LinksTestData.DeleteAsync(factory, world.Items, feature);
        var epicBefore = await LinksTestData.StoredAsync(factory, epic);
        var liveBefore = await LinksTestData.StoredAsync(factory, live);
        var goneBefore = await WorkItemsTestData.ReadAsync(factory, world.Items, gone.Identifier);
        var liveTeam = (await WorkItemsTestData.ReadAsync(factory, world.Items, live.Identifier)).GetProperty("team").GetRawText();

        var answer = await PurgeAsync(world.Items, feature.Identifier);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        var liveAfter = await LinksTestData.StoredAsync(factory, live);
        var goneAfter = await LinksTestData.StoredAsync(factory, gone);
        Assert.Null(liveAfter.ParentId);
        Assert.Null(goneAfter.ParentId);
        Assert.Equal(liveBefore.StateId, liveAfter.StateId);
        Assert.True(liveAfter.ChangedAt > liveBefore.ChangedAt);
        Assert.Equal(liveAfter.ChangedAt, goneAfter.ChangedAt);
        Assert.Equal(liveTeam, (await WorkItemsTestData.ReadAsync(factory, world.Items, live.Identifier)).GetProperty("team").GetRawText());
        var goneNow = await WorkItemsTestData.ReadAsync(factory, world.Items, gone.Identifier);
        Assert.Equal("Removed", goneNow.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
        Assert.Equal(goneBefore.GetProperty("deletion").GetRawText(), goneNow.GetProperty("deletion").GetRawText());
        Assert.Equal(epicBefore, await LinksTestData.StoredAsync(factory, epic));
        Assert.Equal(0, await RowsAsync(feature.Id));
    }

    // US-6 · "The number is never issued again": the highest number is purged, and the next creations take the numbers after it,
    // under any type key.
    [Fact]
    public async Task Purge_TheHighestNumber_NextCreationsGetTheNextNumbers_NoItemEverHasTheNumberAgain_UnderAnyTypeKey()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var identifiers = new List<string>();

        foreach (var title in new[] { "Раз", "Два", "Три", "Четыре", "Пять" })
        {
            identifiers.Add(await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", title));
        }

        var highest = new Item(Guid.Empty, identifiers[^1]);
        Assert.Equal("MSK-TASK-5", highest.Identifier);
        await LinksTestData.DeleteAsync(factory, world, highest);
        Assert.Equal(HttpStatusCode.NoContent, (await PurgeAsync(world, highest.Identifier)).Status);

        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Баг после очистки");
        var task = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Задача после очистки");

        Assert.Equal(["MSK-BUG-6", "MSK-TASK-7"], new[] { bug, task });
        foreach (var identifier in new[] { "MSK-TASK-5", "MSK-BUG-5", "MSK-US-5", "MSK-FEAT-5", "MSK-EPIC-5" })
        {
            LinksTestData.AssertRefused(await WorkItemsTestData.GetAsync(factory, world, identifier), HttpStatusCode.NotFound, NotFoundCode);
        }
    }

    // US-6 · "Only a deleted item can be purged".
    [Fact]
    public async Task Purge_ALiveItem_Is409NotDeleted_NothingRemoved()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Живая", world.PlatformAreaId);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);

        var answer = await PurgeAsync(world.Items, story.Identifier);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.workitem.not_deleted");
        Assert.Equal(before.GetRawText(), (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetRawText());
        Assert.Equal(1, await RowsAsync(story.Id));
    }

    // US-6 · "Only an administrator": a member and a reader are refused by the route's code, the item untouched.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Purge_AMemberAndAViewer_Are403_ItemUntouched(OrganizationRole role)
    {
        var (world, story) = await DeletedStoryAsync();
        var caller = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, role);

        var answer = await PurgeAsync(world.Items, story.Identifier, caller.Token);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        Assert.Equal(1, await RowsAsync(story.Id));
    }

    [Fact]
    public async Task Purge_ANonMember_AndTheInstanceAdministrator_Are404OrganizationNotFound()
    {
        var (world, story) = await DeletedStoryAsync();
        var instanceAdmin = KeycloakTestRealm.CreateToken([.. PermissionCatalog.InstanceCodes], options => options.Subject = OrganizationsTokens.NewSubject());

        foreach (var token in new[] { MembershipTestData.Outsider(), instanceAdmin })
        {
            LinksTestData.AssertRefused(await PurgeAsync(world.Items, story.Identifier, token), HttpStatusCode.NotFound, "organizations.organization.not_found");
        }

        Assert.Equal(1, await RowsAsync(story.Id));
    }

    [Fact]
    public async Task Purge_AnAdministrator_IsNotRefused()
    {
        var (world, story) = await DeletedStoryAsync();
        var admin = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Admin);

        Assert.Equal(HttpStatusCode.NoContent, (await PurgeAsync(world.Items, story.Identifier, admin.Token)).Status);
    }

    // US-6 · "A purge cannot be undone": a restore, a save and a second purge find no row.
    [Fact]
    public async Task AfterAPurge_RestoreSaveAndPurge_Answer404NotFound()
    {
        var (world, story) = await DeletedStoryAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await PurgeAsync(world.Items, story.Identifier)).Status);

        var restore = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, story.Identifier)}/restore", token: OrganizationsTokens.SignedIn());
        var save = await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { title = "Поздно" });
        var again = await PurgeAsync(world.Items, story.Identifier);

        foreach (var answer in new[] { restore, save, again })
        {
            LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, NotFoundCode);
        }
    }

    // US-6 · "Other users' open views meet the purge as an unknown item": a card move and a checklist tick are saves.
    [Fact]
    public async Task AfterAPurge_ACardMoveAndATick_Answer404NotFound()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Карточка", world.PlatformAreaId);
        var child = await BoardsTestData.CreateAsync(factory, world, "TASK", "Пункт");
        await LinksTestData.LinkAsync(factory, world.Items, child, story);
        await LinksTestData.DeleteAsync(factory, world.Items, story);
        await LinksTestData.DeleteAsync(factory, world.Items, child);
        Assert.Equal(HttpStatusCode.NoContent, (await PurgeAsync(world.Items, story.Identifier)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await PurgeAsync(world.Items, child.Identifier)).Status);

        LinksTestData.AssertRefused(await BoardsTestData.SetStateAsync(factory, world, story, "US", "Активна"), HttpStatusCode.NotFound, NotFoundCode);
        LinksTestData.AssertRefused(await BoardsTestData.SetStateAsync(factory, world, child, "TASK", "Закрыта"), HttpStatusCode.NotFound, NotFoundCode);
    }

    // US-6 · "A deactivated project cannot be purged from": the container's not-found, the item untouched.
    [Fact]
    public async Task Purge_UnderADeactivatedProject_Is404ProjectNotFound_ItemUntouched()
    {
        var (world, story) = await DeletedStoryAsync();
        var project = $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}";
        Assert.Equal(HttpStatusCode.NoContent, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/deactivate", token: OrganizationsTokens.SignedIn())).Status);

        var answer = await PurgeAsync(world.Items, story.Identifier);

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal(1, await RowsAsync(story.Id));
    }

    // Unknown, foreign or malformed addresses answer the level's not-found, as Delete's and Restore's do.
    [Fact]
    public async Task Purge_UnknownForeignOrMalformedAddress_Is404TheLevelsCode()
    {
        var (world, story) = await DeletedStoryAsync();

        foreach (var identifier in new[] { "MSK-US-99", "MSK-BUG-" + story.Identifier[(story.Identifier.LastIndexOf('-') + 1)..], "XYZ-US-1", "garbage" })
        {
            LinksTestData.AssertRefused(await PurgeAsync(world.Items, identifier), HttpStatusCode.NotFound, NotFoundCode);
        }

        LinksTestData.AssertRefused(
            await PurgeAsync(world.Items with { ProjectId = Guid.CreateVersion7() }, story.Identifier), HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal(1, await RowsAsync(story.Id));
    }

    // US-6 · "A team-placed item purges": the SQL-only owner key of the team place is met by dropping the place first.
    [Fact]
    public async Task Purge_AnItemWithATeamAndAProjectPlace_DropsBothAndTheItem()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Размещённая", world.PlatformAreaId);
        var other = await BoardsTestData.CreateAsync(factory, world, "US", "Соседняя", world.PlatformAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, story, other);
        await ProjectBoardsTestData.ReorderToAsync(factory, world, story, other);
        await LinksTestData.DeleteAsync(factory, world.Items, story);
        Assert.Equal(1, await OwningTeamTestData.TeamPlacesOfAsync(factory, story.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await PurgeAsync(world.Items, story.Identifier)).Status);

        Assert.Equal(0, await RowsAsync(story.Id));
        Assert.Equal((0, 0), (await OwningTeamTestData.TeamPlacesOfAsync(factory, story.Id), await OwningTeamTestData.ProjectPlacesOfAsync(factory, story.Id)));
        Assert.Equal(0, await OwningTeamTestData.PlacesNamingAnotherTeamAsync(factory, world.ProjectId));
    }

    // --- The trace and the step contract ----------------------------------------------------------------------------------

    /// <summary>A host that collects every log entry and runs the steps a test registers.</summary>
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> HostWith(CapturingLoggerProvider logs, params Action<IServiceCollection>[] registrations) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);

                foreach (var registration in registrations)
                {
                    registration(services);
                }
            }));

    // US-6 · "A purge leaves no trace: no tombstone; one log line without the title; no audit": exactly one line from the
    // handler, with the user, the organization, the project and the identifier, and never the title.
    [Fact]
    public async Task Purge_LeavesNoCopy_AndLogsOneLine_UserOrganizationProjectIdentifier_NeverTheTitle()
    {
        var (world, story) = await DeletedStoryAsync();
        var logs = new CapturingLoggerProvider();
        await using var host = HostWith(logs);

        var answer = await PurgeAsync(world.Items, story.Identifier, host: host);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        var lines = logs.Entries.Where(entry => entry.Category == typeof(Mustask.Application.Features.WorkItems.PurgeWorkItem.PurgeWorkItemHandler).FullName).ToList();
        var line = Assert.Single(lines);
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Contains(story.Identifier, line.Message, StringComparison.Ordinal);
        Assert.Contains(OrganizationsTestData.Creator, line.Message, StringComparison.Ordinal);
        Assert.Contains(world.ProjectId.ToString(), line.Message, StringComparison.Ordinal);
        Assert.Contains(world.OrganizationId.ToString(), line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("На удаление", string.Join(" | ", logs.Entries.Select(entry => entry.Message)), StringComparison.Ordinal);
        Assert.DoesNotContain("Размещённая", line.Message, StringComparison.Ordinal);
        Assert.Equal(0, await RowsAsync(story.Id));
    }

    // Security review S-1 · BR-DR-15: a purge removes the only record of who deleted the item, so the one line names the deleter (here
    // a member, not the purging administrator) and the deletion's time as the card showed them before the purge.
    [Fact]
    public async Task Purge_TheLogLineNamesWhoDeletedTheItemAndWhen_NotOnlyWhoPurged()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "На удаление", world.PlatformAreaId);
        var deleter = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var deleted = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, story.Identifier)}/delete", token: deleter.Token);
        Assert.Equal(HttpStatusCode.OK, deleted.Status);
        var deletedAt = deleted.Body.GetProperty("deletion").GetProperty("at").GetDateTimeOffset();
        var logs = new CapturingLoggerProvider();
        await using var host = HostWith(logs);

        var answer = await PurgeAsync(world.Items, story.Identifier, host: host);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        var line = Assert.Single(logs.Entries, entry => entry.Category == typeof(Mustask.Application.Features.WorkItems.PurgeWorkItem.PurgeWorkItemHandler).FullName);
        Assert.Contains(OrganizationsTestData.Creator, line.Message, StringComparison.Ordinal);
        Assert.Contains(deleter.Id, line.Message, StringComparison.Ordinal);
        Assert.Contains($"{deletedAt.ToUniversalTime():O}", line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("На удаление", line.Message, StringComparison.Ordinal);
    }

    // The same line for an item in «Удалена» with no marks (BR-DR-10): there is nobody to name, the purge goes through and logs once.
    [Fact]
    public async Task Purge_AnItemWithNoMarks_Is204_AndLogsOneLine()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Без отметок", world.PlatformAreaId);
        await LinksTestData.PutInRemovedWithNoMarksAsync(factory, world.Items, story);
        var logs = new CapturingLoggerProvider();
        await using var host = HostWith(logs);

        var answer = await PurgeAsync(world.Items, story.Identifier, host: host);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        var line = Assert.Single(logs.Entries, entry => entry.Category == typeof(Mustask.Application.Features.WorkItems.PurgeWorkItem.PurgeWorkItemHandler).FullName);
        Assert.Contains(story.Identifier, line.Message, StringComparison.Ordinal);
        Assert.Equal(0, await RowsAsync(story.Id));
    }

    /// <summary>The recorder every <see cref="RecordingStep"/> of a test writes to, shared by the host's scopes.</summary>
    public sealed class StepRecorder
    {
        public ConcurrentQueue<string> Calls { get; } = new();

        public ConcurrentQueue<IReadOnlyList<Guid>> Children { get; } = new();

        public bool Fail { get; set; }

        public bool Refuse { get; set; }

        public int ItemRowsSeenByTheStep { get; set; } = -1;
    }

    /// <summary>
    /// A purge step of a line to come: it refuses in its pre-flight when told to; inside the transaction it writes on the
    /// survivors — it retitles the children it is handed, through the request's own context, so a rollback must undo it —
    /// reads whether the item still has its row, and fails when told to. (It never writes the item's own row: the purge holds
    /// that row tracked, and a step has no business with it.)
    /// </summary>
    public sealed class RecordingStep(StepRecorder recorder, MustaskDbContext context) : IWorkItemPurgeStep
    {
        public Task PreflightAsync(WorkItemPurgeRequest request, CancellationToken cancellationToken)
        {
            recorder.Calls.Enqueue("preflight");

            return recorder.Refuse ? throw new ConflictException("test.conflict") : Task.CompletedTask;
        }

        public async Task PurgeAsync(WorkItemPurge purge, CancellationToken cancellationToken)
        {
            recorder.Calls.Enqueue("purge");
            recorder.Children.Enqueue(purge.ChildIds);

            var children = purge.ChildIds.ToArray();

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE work_items SET title = 'изменено шагом' WHERE id = ANY({children})", cancellationToken);
            recorder.ItemRowsSeenByTheStep = await context.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM work_items WHERE id = {purge.Request.WorkItemId}")
                .SingleAsync(cancellationToken);

            if (recorder.Fail)
            {
                throw new InvalidOperationException("The step failed.");
            }
        }
    }

    // US-6 · "The step contract end to end": a registered step runs inside the transaction, sees the item still there and the
    // children's ids, and its failure rolls everything back — the item, its place, its children's link, and the log line.
    [Fact]
    public async Task Purge_RunsARegisteredStep_InsideTheTransaction_AndItsFailureRollsEverythingBack()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var child = await BoardsTestData.CreateAsync(factory, world, "US", "Дочерняя", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, child, feature);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, child);
        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Bottom(feature), "features");
        await LinksTestData.DeleteAsync(factory, world.Items, feature);
        Assert.Equal(1, await OwningTeamTestData.TeamPlacesOfAsync(factory, feature.Id));
        var childBefore = await LinksTestData.StoredAsync(factory, child);
        var recorder = new StepRecorder { Fail = true };
        var logs = new CapturingLoggerProvider();
        await using var host = HostWith(
            logs,
            services =>
            {
                services.AddSingleton(recorder);
                services.AddScoped<IWorkItemPurgeStep, RecordingStep>();
            });

        var failed = await PurgeAsync(world.Items, feature.Identifier, host: host);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.Status);
        Assert.Equal(["preflight", "purge"], recorder.Calls);
        Assert.Equal([child.Id], Assert.Single(recorder.Children));
        Assert.Equal(1, recorder.ItemRowsSeenByTheStep);

        // Everything is as it was: the item, its place, the child's link and «Изменён», and the title the step rewrote.
        Assert.Equal(1, await RowsAsync(feature.Id));
        Assert.Equal(1, await OwningTeamTestData.TeamPlacesOfAsync(factory, feature.Id));
        Assert.Equal("Дочерняя", (await WorkItemsTestData.ReadAsync(factory, world.Items, child.Identifier)).GetProperty("title").GetString());
        Assert.Equal(childBefore, await LinksTestData.StoredAsync(factory, child));
        Assert.DoesNotContain(
            logs.Entries,
            entry => entry.Category == typeof(Mustask.Application.Features.WorkItems.PurgeWorkItem.PurgeWorkItemHandler).FullName);

        // The same host, the step no longer failing: the purge goes through, and the step has seen the children once more.
        recorder.Fail = false;
        var done = await PurgeAsync(world.Items, feature.Identifier, host: host);

        Assert.Equal(HttpStatusCode.NoContent, done.Status);
        Assert.Equal(0, await RowsAsync(feature.Id));
        Assert.Null((await LinksTestData.StoredAsync(factory, child)).ParentId);
        Assert.Equal("изменено шагом", (await WorkItemsTestData.ReadAsync(factory, world.Items, child.Identifier)).GetProperty("title").GetString());
    }

    // US-6 · "The step contract end to end", the other half: a pre-flight that refuses is the purge's 409, and nothing ran.
    [Fact]
    public async Task Purge_APreflightRefusal_Is409_NothingRemoved_NoStepRan()
    {
        var (world, story) = await DeletedStoryAsync();
        var recorder = new StepRecorder { Refuse = true };
        await using var host = HostWith(
            new CapturingLoggerProvider(),
            services =>
            {
                services.AddSingleton(recorder);
                services.AddScoped<IWorkItemPurgeStep, RecordingStep>();
            });

        var answer = await PurgeAsync(world.Items, story.Identifier, host: host);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "test.conflict");
        Assert.Equal(["preflight"], recorder.Calls);
        Assert.Equal(1, await RowsAsync(story.Id));
        Assert.Equal("Removed", (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("state").GetProperty("category").GetProperty("code").GetString());
    }
}
