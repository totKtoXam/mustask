using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// The iterations tests' data and requests (mod-007). Every test builds its own world: the boards world (organization, project
/// «Мастаск» keyed MSK, the teams «Мастаск» and «Платформа» with the area «Платформа»), whose items are created through mod-003's
/// endpoints and owned by «Платформа» when put in its area. Sprints are made through the sprint endpoints, so what a test reads is
/// what the API wrote; counts and the like are asserted on the answers, never on the tables (except what no endpoint shows).
/// </summary>
internal static class IterationsTestData
{
    public static Task<BoardsWorld> CreateWorldAsync(MustaskApiFactory factory) => BoardsTestData.CreateWorldAsync(factory);

    public static string Sprints(BoardsWorld world, Guid teamId) => $"/api/organizations/{world.OrganizationId}/teams/{teamId}/sprints";

    public static string Sprint(BoardsWorld world, Guid teamId, Guid sprintId) => $"{Sprints(world, teamId)}/{sprintId}";

    public static Task<Answer> SendAsync(
        MustaskApiFactory factory, HttpMethod method, string path, object? body = null, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token ?? OrganizationsTokens.SignedIn(), gate);

    public static Task<Answer> CreateAsync(
        MustaskApiFactory factory, BoardsWorld world, Guid teamId, string? name = null, string? start = null, string? end = null, string? token = null) =>
        SendAsync(factory, HttpMethod.Post, Sprints(world, teamId), new { name, startDate = start, endDate = end }, token);

    /// <summary>A created sprint's id; fails the test unless the creation answers 201.</summary>
    public static async Task<Guid> NewAsync(
        MustaskApiFactory factory, BoardsWorld world, Guid teamId, string? name = null, string? start = null, string? end = null)
    {
        var answer = await CreateAsync(factory, world, teamId, name, start, end);
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return answer.Body.GetProperty("id").GetGuid();
    }

    public static Task<Answer> StartAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, Guid sprintId, string? token = null) =>
        SendAsync(factory, HttpMethod.Post, $"{Sprint(world, teamId, sprintId)}/start", null, token);

    public static Task<Answer> CloseAsync(
        MustaskApiFactory factory, BoardsWorld world, Guid teamId, Guid sprintId, Guid? destination = null, string? token = null, CommitGate? gate = null) =>
        SendAsync(
            factory,
            HttpMethod.Post,
            $"{Sprint(world, teamId, sprintId)}/close",
            new { destinationSprintId = destination?.ToString() },
            token,
            gate);

    /// <summary>A Planned sprint with dates, started; fails the test unless it becomes Active.</summary>
    public static async Task<Guid> ActiveAsync(
        MustaskApiFactory factory, BoardsWorld world, Guid teamId, string? name = null, string start = "2026-11-02", string end = "2026-11-13")
    {
        var id = await NewAsync(factory, world, teamId, name, start, end);
        var started = await StartAsync(factory, world, teamId, id);
        Assert.Equal(HttpStatusCode.OK, started.Status);

        return id;
    }

    public static async Task<Answer> ReadAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, Guid sprintId, string tail = "")
    {
        var answer = await SendAsync(factory, HttpMethod.Get, Sprint(world, teamId, sprintId) + tail);
        Assert.True(answer.Status == HttpStatusCode.OK, $"{tail}: {answer.Status} {answer.ErrorCode}");

        return answer;
    }

    /// <summary>An item of the team «Платформа» (a story unless said otherwise), optionally with story points, state, assignee and sprint.</summary>
    public static async Task<Item> ItemAsync(
        MustaskApiFactory factory,
        BoardsWorld world,
        string title,
        decimal? storyPoints = null,
        string? stateName = null,
        string? assignee = null,
        Guid? sprintId = null,
        string typeKey = "US")
    {
        var item = await BoardsTestData.CreateAsync(factory, world, typeKey, title, world.PlatformAreaId);

        if (storyPoints is { } points)
        {
            var saved = await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { storyPoints = new { value = points } });
            Assert.Equal(HttpStatusCode.OK, saved.Status);
        }

        if (assignee is not null)
        {
            var saved = await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { assignee = new { userId = assignee } });
            Assert.Equal(HttpStatusCode.OK, saved.Status);
        }

        if (stateName is not null)
        {
            var moved = await BoardsTestData.SetStateAsync(factory, world, item, typeKey, stateName);
            Assert.Equal(HttpStatusCode.OK, moved.Status);
        }

        if (sprintId is { } sprint)
        {
            var set = await SetSprintAsync(factory, world, item, sprint);
            Assert.Equal(HttpStatusCode.OK, set.Status);
        }

        return item;
    }

    public static Task<Answer> SetSprintAsync(MustaskApiFactory factory, BoardsWorld world, Item item, Guid? sprintId, CommitGate? gate = null) =>
        WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { sprint = new { sprintId = sprintId?.ToString() } }, gate);

    /// <summary>The item's sprint as its card shows it: <c>null</c> or the sprint's id.</summary>
    public static async Task<Guid?> SprintOfAsync(MustaskApiFactory factory, BoardsWorld world, Item item)
    {
        var card = await WorkItemsTestData.ReadAsync(factory, world.Items, item.Identifier);

        return card.GetProperty("sprint") is { ValueKind: System.Text.Json.JsonValueKind.Object } sprint ? sprint.GetProperty("id").GetGuid() : null;
    }

    /// <summary>The item's sprint pointer as the table says it, for what the card does not show.</summary>
    public static async Task<Guid?> StoredSprintAsync(MustaskApiFactory factory, Item item)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Mustask.Infrastructure.MustaskDbContext>();

        return await context.WorkItems.Where(row => row.Id == item.Id).Select(row => row.SprintId).SingleAsync();
    }

    /// <summary>Deletes an item (mod-005): into «Удалена», where the sprint keeps it and counts it nowhere.</summary>
    public static Task<Answer> DeleteItemAsync(MustaskApiFactory factory, BoardsWorld world, Item item) =>
        SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, item.Identifier)}/delete");

    /// <summary>Purges a deleted item for good (mod-005): the sprint's close record keeps its line.</summary>
    public static Task<Answer> PurgeItemAsync(MustaskApiFactory factory, BoardsWorld world, Item item) =>
        SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, item.Identifier)}/purge");

    /// <summary>A team grant held by a fresh member of the organization (Viewer unless said otherwise), with a token.</summary>
    public static async Task<MembershipTestData.TestMember> HolderAsync(
        MustaskApiFactory factory, BoardsWorld world, Guid teamId, GrantRole? role, OrganizationRole organizationRole = OrganizationRole.Viewer)
    {
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, organizationRole);

        if (role is { } granted)
        {
            await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
            {
                await services.GetRequiredService<IGrantRepository>()
                    .AddAsync(ResourceScope.Team, world.OrganizationId, teamId, member.Id, granted, "seed", OrganizationsTestData.Now, ct);

                return true;
            });
        }

        return member;
    }
}
