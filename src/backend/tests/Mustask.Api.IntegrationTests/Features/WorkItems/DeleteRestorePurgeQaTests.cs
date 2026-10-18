using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Process;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 QA pass · the cases the developer's suites leave out, written from the spec before the code was read: the hierarchy's
/// checks count stored links through a deleted item (BR-DR-10), an organization that is deactivated answers its own not-found
/// for all three routes (BR-DR-17, ADR-0030 rule 8), the deleter's two marks when the user left or the directory does not know
/// them (api-contract § 1.1, BR-13), the address's type key, the body of the three routes, and a storm of mixed writes that
/// must end with no server error and with the marks agreeing with the state's category.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteRestorePurgeQaTests(MustaskApiFactory factory)
{
    private Task<Answer> PostAsync(WorkItemsWorld world, string identifier, string action, object? body = null, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world, identifier)}/{action}", body, token ?? OrganizationsTokens.SignedIn());

    private async Task<Item> TaskAsync(WorkItemsWorld world, string title)
    {
        var answer = await WorkItemsTestData.CreateAsync(factory, world, "TASK", title);
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    // --- BR-DR-10 · "the cycle and depth checks of other writes count stored links through [a deleted item]" ----------------------

    // A task chain T1 > T2 > T3 with T2 deleted: T1 beneath T3 would close a cycle through the deleted T2. The write is refused, and
    // a restore of T2 afterwards meets a chain without a cycle (which a restore would never be refused for).
    [Fact]
    public async Task Cycle_ThroughADeletedItem_IsStillRefused_AndTheRestoreFindsNoCycle()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var top = await BoardsTestData.CreateAsync(factory, world, "TASK", "Верх", world.PlatformAreaId);
        var middle = await BoardsTestData.CreateAsync(factory, world, "TASK", "Середина", world.PlatformAreaId);
        var bottom = await BoardsTestData.CreateAsync(factory, world, "TASK", "Низ", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, middle, top);
        await LinksTestData.LinkAsync(factory, world.Items, bottom, middle);
        await LinksTestData.DeleteAsync(factory, world.Items, middle);

        var refused = await LinksTestData.SetParentAsync(factory, world.Items, top.Identifier, bottom.Id, null);

        LinksTestData.AssertRefused(refused, HttpStatusCode.Conflict, "workitems.link.cycle");
        Assert.Null(await LinksTestData.StoredParentAsync(factory, top));

        var restored = await PostAsync(world.Items, middle.Identifier, "restore");
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(top.Id, await LinksTestData.StoredParentAsync(factory, middle));
        Assert.Equal(middle.Id, await LinksTestData.StoredParentAsync(factory, bottom));
    }

    // A moved subtree is as tall as its stored links, a deleted member included: 25 beneath a 40-chain make 65 even with the
    // 11th deleted (without it the 10 live items above it would fit).
    [Fact]
    public async Task Depth_OfAMovedSubtree_CountsADeletedMember_Refused409()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var parentChain = await BulkItemsTestData.InsertChainAsync(factory, world, world.RootAreaId, 40);
        var tooTall = await BulkItemsTestData.InsertChainAsync(factory, world, world.RootAreaId, 25, firstNumber: 41);
        await LinksTestData.DeleteAsync(factory, world, tooTall[10]);

        var refused = await LinksTestData.SetParentAsync(factory, world, tooTall[0].Identifier, parentChain[^1].Id, null);

        LinksTestData.AssertRefused(refused, HttpStatusCode.Conflict, "workitems.link.depth_exceeded");
        Assert.Null(await LinksTestData.StoredParentAsync(factory, tooTall[0]));
    }

    // A parent chain of 64 whose 31st item is deleted still holds 64 stored links: a task beneath its tail would be the 65th.
    [Fact]
    public async Task Depth_OfAParentChain_CountsADeletedAncestor_Refused409()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var chain = await BulkItemsTestData.InsertChainAsync(factory, world, world.RootAreaId, 64);
        var over = await TaskAsync(world, "Шестьдесят пятое");
        await LinksTestData.DeleteAsync(factory, world, chain[30]);

        var refused = await LinksTestData.SetParentAsync(factory, world, over.Identifier, chain[^1].Id, null);

        LinksTestData.AssertRefused(refused, HttpStatusCode.Conflict, "workitems.link.depth_exceeded");
    }

    // --- BR-DR-17 · containment ------------------------------------------------------------------------------------------------

    // An organization that is deactivated answers its own not-found to Delete, Restore and Purge, deleted items included, and
    // nothing changes; restored, its items are as they were (ADR-0026 rules 3 and 5).
    [Fact]
    public async Task DeactivatedOrganization_AllThreeRoutes_AnswerOrganizationNotFound_NothingChanges()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var live = await BoardsTestData.CreateAsync(factory, world, "US", "Живая", world.PlatformAreaId);
        var gone = await BoardsTestData.CreateAsync(factory, world, "US", "Удалённая", world.PlatformAreaId);
        await LinksTestData.DeleteAsync(factory, world.Items, gone);
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var organization = OrganizationsApi.Organization(world.OrganizationId);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{organization}/deactivate", token: OrganizationsTokens.SignedIn())).Status);

        foreach (var answer in new[]
        {
            await PostAsync(world.Items, live.Identifier, "delete"),
            await PostAsync(world.Items, gone.Identifier, "restore"),
            await PostAsync(world.Items, gone.Identifier, "purge"),
        })
        {
            Assert.Equal(HttpStatusCode.NotFound, answer.Status);
            Assert.True(
                answer.ErrorCode is "organizations.organization.not_found",
                $"A deactivated organization answered {answer.Status} {answer.ErrorCode}.");
        }

        Assert.Equal(
            HttpStatusCode.OK,
            (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{organization}/restore", token: OrganizationsTokens.SignedIn())).Status);
        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        Assert.Equal(JsonValueKind.Object, (await WorkItemsTestData.ReadAsync(factory, world.Items, gone.Identifier)).GetProperty("deletion").ValueKind);
    }

    // BR-DR-17 / mod-003 D-5 · an identifier whose type key is not the item's names no item, on all three routes.
    [Fact]
    public async Task AnIdentifierWithAnotherTypeKey_IsNotFound_OnAllThreeRoutes()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача", world.PlatformAreaId);
        await LinksTestData.DeleteAsync(factory, world.Items, task);
        var wrongKey = task.Identifier.Replace("-TASK-", "-US-", StringComparison.Ordinal);

        foreach (var action in new[] { "delete", "restore", "purge" })
        {
            LinksTestData.AssertRefused(await PostAsync(world.Items, wrongKey, action), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        }

        Assert.Equal(JsonValueKind.Object, (await WorkItemsTestData.ReadAsync(factory, world.Items, task.Identifier)).GetProperty("deletion").ValueKind);
    }

    // --- api-contract § 1.1 · the deleter's marks ----------------------------------------------------------------------------

    // BR-13 · the deleter who has left the organization keeps their name and activity but is no longer a member; one the directory
    // has disabled is inactive; one it never knew has no name and is inactive; none of them changes the deletion.
    [Fact]
    public async Task DeletionBy_CarriesTheTwoMarks_ForAFormerMember_ADisabledUser_AndAnUnknownOne()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var leaver = await MembershipTestData.NewNamedMemberAsync(factory, world.OrganizationId, OrganizationRole.Member, "Лев", "Ушёл");
        var disabledId = await WorkItemsTestData.AddUserAsync(factory, "Дана", "Отключена", active: false);
        await MembershipTestData.AddMemberAsync(factory, world.OrganizationId, disabledId, OrganizationRole.Member);
        var ghost = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);

        var byLeaver = await TaskAsync(world, "Удалил ушедший");
        var byDisabled = await TaskAsync(world, "Удалил отключённый");
        var byGhost = await TaskAsync(world, "Удалил неизвестный");
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(world, byLeaver.Identifier, "delete", token: leaver.Token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(world, byDisabled.Identifier, "delete", token: OrganizationsTokens.SignedIn(disabledId))).Status);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(world, byGhost.Identifier, "delete", token: ghost.Token)).Status);
        Assert.True(await MembershipTestData.RemoveMemberAsync(factory, world.OrganizationId, leaver.Id));

        async Task<(string? Name, bool Active, bool Member)> ByAsync(Item item)
        {
            var by = (await WorkItemsTestData.ReadAsync(factory, world, item.Identifier)).GetProperty("deletion").GetProperty("by");

            return (by.GetProperty("displayName").GetString(), by.GetProperty("isActive").GetBoolean(), by.GetProperty("isMember").GetBoolean());
        }

        Assert.Equal(("Лев Ушёл", true, false), await ByAsync(byLeaver));
        Assert.Equal(("Дана Отключена", false, true), await ByAsync(byDisabled));
        Assert.Equal((null, false, true), await ByAsync(byGhost));
    }

    // --- The body of the three routes (api-contract § 0.4: "None of the three reads a body before step 6") ----------------------

    private async Task<(HttpStatusCode Status, string? Code)> RawAsync(WorkItemsWorld world, string identifier, string action, string? body, string contentType)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{WorkItemsTestData.Item(world, identifier)}/{action}");
        request.WithBearer(OrganizationsTokens.SignedIn());

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue(contentType));
        }

        using var response = await factory.CreateClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var code = text.Length > 0 && text.StartsWith('{') ? JsonDocument.Parse(text).RootElement.TryGetProperty("errorCode", out var c) ? c.GetString() : null : null;

        return (response.StatusCode, code);
    }

    // Delete and Purge take no body: junk of any type is ignored, as a missing one is.
    [Fact]
    public async Task DeleteAndPurge_IgnoreABody_OfAnyShapeOrType()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var first = await TaskAsync(world, "Первая");
        var second = await TaskAsync(world, "Вторая");

        Assert.Equal((HttpStatusCode.OK, null), await RawAsync(world, first.Identifier, "delete", "{ not json", "application/json"));
        Assert.Equal((HttpStatusCode.OK, null), await RawAsync(world, second.Identifier, "delete", "plain text", "text/plain"));
        Assert.Equal((HttpStatusCode.NoContent, null), await RawAsync(world, first.Identifier, "purge", "{ not json", "application/json"));
        Assert.Equal((HttpStatusCode.NoContent, null), await RawAsync(world, second.Identifier, "purge", "{\"stateId\":1}", "application/json"));
    }

    // Restore: a body with no stateId, an empty body with a JSON type, and an unknown member are all "the held state"; a malformed or
    // mistyped body is refused before anything changes. Which refusal is recorded, and only that nothing is applied is required.
    [Fact]
    public async Task Restore_BodyShapes_OnlyAWellFormedBodyRestores_AMalformedOneChangesNothing()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var item = await TaskAsync(world, "Задача");
        await LinksTestData.DeleteAsync(factory, world, item);

        foreach (var (body, type) in new[] { ("{ not json", "application/json"), ("{\"stateId\":5}", "application/json"), ("{\"stateId\":{}}", "application/json"), ("plain", "text/plain") })
        {
            var (status, code) = await RawAsync(world, item.Identifier, "restore", body, type);

            // Recorded: 400 http.bad_request for a malformed or mistyped JSON body, 415 http.request_failed for a body that is not JSON.
            Assert.True(status is HttpStatusCode.BadRequest or HttpStatusCode.UnsupportedMediaType, $"{body} ({type}): {status} {code}.");
            Assert.Equal(
                "Removed",
                (await WorkItemsTestData.ReadAsync(factory, world, item.Identifier)).GetProperty("state").GetProperty("category").GetProperty("code").GetString());
        }

        Assert.Equal((HttpStatusCode.OK, null), await RawAsync(world, item.Identifier, "restore", "{\"unknown\":1}", "application/json"));
    }

    // --- BR-DR-11 · the pairs in bulk ------------------------------------------------------------------------------------------

    // Eight parallel requests of one kind on one item: Delete — one 200, the rest 409 deleted; Restore — one 200, the rest 409
    // not_deleted; Purge — one 204, the rest 404 (the row is gone). No server error, whatever the order.
    [Fact]
    public async Task EightParallelRequestsOfOneKind_ExactlyOneWins_TheRestAnswerTheirCode()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var item = await TaskAsync(world, "Гонка");

        var deletes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostAsync(world, item.Identifier, "delete")));
        Assert.Equal(1, deletes.Count(answer => answer.Status == HttpStatusCode.OK));
        Assert.All(deletes.Where(answer => answer.Status != HttpStatusCode.OK), answer => LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.workitem.deleted"));

        var restores = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostAsync(world, item.Identifier, "restore")));
        Assert.Equal(1, restores.Count(answer => answer.Status == HttpStatusCode.OK));
        Assert.All(restores.Where(answer => answer.Status != HttpStatusCode.OK), answer => LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.workitem.not_deleted"));

        await LinksTestData.DeleteAsync(factory, world, item);
        var purges = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostAsync(world, item.Identifier, "purge")));
        Assert.Equal(1, purges.Count(answer => answer.Status == HttpStatusCode.NoContent));
        Assert.All(purges.Where(answer => answer.Status != HttpStatusCode.NoContent), answer => LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "workitems.workitem.not_found"));
    }

    // A storm of Delete, Restore (held and chosen), Save, card move, Purge and a child's mapping on ten items at once, three rounds:
    // every answer is a success or a refusal the contract names, none is a server error, and what is stored agrees — a live item
    // carries no mark, a deleted one carries all three (every deletion here went through the route).
    [Fact]
    public async Task Storm_OfMixedWrites_NoServerError_MarksAgreeWithTheStatesCategory()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var stories = new List<Item>();
        var tasks = new List<Item>();

        for (var index = 0; index < 6; index++)
        {
            var answer = await WorkItemsTestData.CreateAsync(factory, world, "US", $"История {index}");
            stories.Add(new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!));
        }

        for (var index = 0; index < 4; index++)
        {
            var task = await TaskAsync(world, $"Задача {index}");
            await LinksTestData.LinkAsync(factory, world, task, stories[index]);
            tasks.Add(task);
        }

        var everyone = stories.Concat(tasks).ToArray();
        var states = world.Types["US"];
        var random = new Random(20261018);
        var allowed = new HashSet<string?>
        {
            "workitems.workitem.deleted", "workitems.workitem.not_deleted", "workitems.workitem.state_not_allowed",
            "workitems.workitem.not_found", "workitems.link.item_deleted", "workitems.link.parent_changed", "workitems.link.parent_not_found",
        };
        var violations = new List<string>();

        for (var round = 0; round < 3; round++)
        {
            var requests = Enumerable.Range(0, 60).Select(_ =>
            {
                var item = everyone[random.Next(everyone.Length)];
                var kind = random.Next(100);
                var isTask = tasks.Contains(item);

                Task<Answer> send = kind switch
                {
                    < 25 => PostAsync(world, item.Identifier, "delete"),
                    < 45 => PostAsync(world, item.Identifier, "restore"),
                    < 55 => PostAsync(world, item.Identifier, "restore", new { stateId = states.StateId("Новая").ToString() }),
                    < 70 => WorkItemsTestData.SaveAsync(factory, world, item.Identifier, new { title = $"Новое {random.Next(1000)}" }),
                    < 80 => WorkItemsTestData.SaveAsync(factory, world, item.Identifier, new { stateId = world.Types[isTask ? "TASK" : "US"].StateId("Активна").ToString() }),
                    < 85 => PostAsync(world, item.Identifier, "purge"),
                    _ when isTask => LinksTestData.SetParentAsync(factory, world, item.Identifier, stories[random.Next(stories.Count)].Id, null),
                    _ => PostAsync(world, item.Identifier, "delete"),
                };

                return send;
            }).ToArray();

            foreach (var answer in await Task.WhenAll(requests))
            {
                var ok = (int)answer.Status is >= 200 and < 300;

                if (!ok && !(answer.Status is HttpStatusCode.Conflict or HttpStatusCode.NotFound && allowed.Contains(answer.ErrorCode)))
                {
                    violations.Add($"round {round}: {(int)answer.Status} {answer.ErrorCode}");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("; ", violations.Distinct()));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var stored = await (
                from item in db.WorkItems.AsNoTracking()
                join state in db.StateDefinitions on item.StateId equals state.Id
                where item.ProjectId == world.ProjectId
                select new { item.Number, state.Category, item.DeletedFromStateId, item.DeletedBy, item.DeletedAt })
            .ToListAsync();

        foreach (var row in stored)
        {
            var marks = new[] { row.DeletedFromStateId is not null, row.DeletedBy is not null, row.DeletedAt is not null };

            if (row.Category == StateCategory.Removed)
            {
                Assert.True(marks.All(mark => mark), $"Item {row.Number} is deleted and lacks a mark.");
            }
            else
            {
                Assert.True(marks.All(mark => !mark), $"Item {row.Number} is live and carries a mark.");
            }
        }
    }
}
