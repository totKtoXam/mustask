using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.Process;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-016 US-2 · "A parent write needs the link code as well" (api-contract § 4.2, "The second code of rows 29 and 31", feat-001
/// BR-H-18, feat-027 BR-T-17). The route declares <c>workitems.workitem.write</c>, its one code; a create or a save that carries a
/// parent also needs <c>workitems.link.write</c>, checked against the same role bundle <b>first</b> — before the project, the item or
/// the parent is looked at and before any 400. No role of D-8 has a bundle that holds the first code and not the second, so the
/// tests run on a host whose <see cref="IOrganizationAccess"/> says so: the scoped check still admits the caller as a member (their
/// real bundle holds the route's code), and the handler then asks the access it was given.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ParentWriteSecondCodeTests(MustaskApiFactory factory)
{
    private const string ForbiddenCode = "organizations.access.forbidden";

    /// <summary>A role that writes work items but may not write links: the member's bundle without <c>workitems.link.write</c>.</summary>
    private sealed class WritesButDoesNotLink : IOrganizationAccess
    {
        public OrganizationRole Role => OrganizationRole.Member;

        public bool Holds(string code) => code != PermissionCatalog.WorkItemsLinkWrite && OrganizationRoleBundles.Holds(Role, code);
    }

    private DerivedHost StartDerivedHost() =>
        new(factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IOrganizationAccess, WritesButDoesNotLink>())));

    /// <summary>The derived host and its client; disposing it disposes the host.</summary>
    private sealed class DerivedHost(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host) : IDisposable
    {
        public HttpClient Client { get; } = host.CreateClient();

        public void Dispose() => host.Dispose();
    }

    private static async Task<Answer> SendAsync(HttpClient client, HttpMethod method, string path, object body, string token)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body, body.GetType()) };
        request.WithBearer(token);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();

        return new Answer(response.StatusCode, response.Headers.Location, response.Content.Headers.ContentType?.MediaType, json);
    }

    // US-2 · a create carrying a parent: 403 «Недостаточно прав» before any refusal of the project or of the parent, before any 400
    // (an empty title, a malformed submission key), and nothing is created — whatever the parent and the project say.
    [Fact]
    public async Task Create_WithAParent_WhenTheBundleLacksTheLinkCode_Is403Before_TheProject_TheParent_AndTheValues()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var realParent = (await WorkItemsTestData.CreateAsync(factory, world, "US", "Родитель")).Body.GetProperty("id").GetString()!;
        var storedBefore = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var typeId = world.Types["TASK"].Id.ToString();

        using var loggerGuard = new StaticLoggerGuard();
        using var derived = StartDerivedHost();
        var path = WorkItemsTestData.List(world);
        var nonexistentProject = $"/api/organizations/{world.OrganizationId}/projects/{Guid.CreateVersion7()}/workitems";

        var withARealParent = await SendAsync(
            derived.Client, HttpMethod.Post, path, new { requestId = Guid.NewGuid().ToString(), typeId, title = "Задача", parentId = realParent }, member.Token);
        var withNoSuchParent = await SendAsync(
            derived.Client, HttpMethod.Post, path,
            new { requestId = Guid.NewGuid().ToString(), typeId, title = "Задача", parentId = Guid.CreateVersion7().ToString() },
            member.Token);
        var withBadValues = await SendAsync(
            derived.Client, HttpMethod.Post, path, new { requestId = "abc", typeId = "abc", title = "   ", parentId = "abc" }, member.Token);
        var inANonexistentProject = await SendAsync(
            derived.Client, HttpMethod.Post, nonexistentProject,
            new { requestId = Guid.NewGuid().ToString(), typeId, title = "Задача", parentId = Guid.CreateVersion7().ToString() },
            member.Token);

        foreach (var answer in new[] { withARealParent, withNoSuchParent, withBadValues, inANonexistentProject })
        {
            Assert.Equal((HttpStatusCode.Forbidden, ForbiddenCode), (answer.Status, answer.ErrorCode));
            Assert.Equal("Недостаточно прав", answer.Detail);
        }

        Assert.Equal(storedBefore, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // US-2 · "the same save without a parent succeeds": the second code is for a parent write only — a create without a parent, and a
    // save of other fields, answer as for any member.
    [Fact]
    public async Task CreateAndSave_WithoutAParent_Succeed_WhenTheBundleLacksTheLinkCode()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);

        using var loggerGuard = new StaticLoggerGuard();
        using var derived = StartDerivedHost();

        var created = await SendAsync(
            derived.Client, HttpMethod.Post, WorkItemsTestData.List(world),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Types["TASK"].Id.ToString(), title = "Без родителя" },
            member.Token);
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var identifier = created.Body.GetProperty("identifier").GetString()!;

        var saved = await SendAsync(
            derived.Client, HttpMethod.Patch, WorkItemsTestData.Item(world, identifier), new { title = "Без родителя, уточнено" }, member.Token);

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal("Без родителя, уточнено", (await WorkItemsTestData.ReadAsync(factory, world, identifier)).GetProperty("title").GetString());
    }

    // US-2 · a save carrying a parent (a parent set, or cleared with `parentId: null`): 403 before the project, the item, the parent
    // and the values are looked at; nothing changes.
    [Fact]
    public async Task Save_WithAParent_WhenTheBundleLacksTheLinkCode_Is403Before_TheItem_TheParent_AndTheValues()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var parent = (await WorkItemsTestData.CreateAsync(factory, world, "US", "Родитель")).Body.GetProperty("id").GetString()!;
        var task = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Ребёнок");
        var storedBefore = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        using var loggerGuard = new StaticLoggerGuard();
        using var derived = StartDerivedHost();

        var attempts = new (string Name, string Path, object Body)[]
        {
            ("a parent set", WorkItemsTestData.Item(world, task), new { parent = new { parentId = parent } }),
            ("a parent cleared", WorkItemsTestData.Item(world, task), new { parent = new { parentId = (string?)null } }),
            ("a parent with bad values", WorkItemsTestData.Item(world, task), new { title = new string('x', 1000), parent = new { parentId = "abc" } }),
            ("an item that does not exist", WorkItemsTestData.Item(world, "MSK-TASK-99"), new { parent = new { parentId = Guid.CreateVersion7().ToString() } }),
            (
                "a project that does not exist",
                $"/api/organizations/{world.OrganizationId}/projects/{Guid.CreateVersion7()}/workitems/MSK-TASK-1",
                new { parent = new { parentId = Guid.CreateVersion7().ToString() } }),
        };

        foreach (var (name, path, body) in attempts)
        {
            var answer = await SendAsync(derived.Client, HttpMethod.Patch, path, body, member.Token);

            Assert.True(
                answer.Status == HttpStatusCode.Forbidden && answer.ErrorCode == ForbiddenCode,
                $"A save with {name} answered {(int)answer.Status} {answer.ErrorCode}.");
        }

        Assert.Equal(storedBefore, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        Assert.Equal(JsonValueKind.Null, (await WorkItemsTestData.ReadAsync(factory, world, task)).GetProperty("parent").ValueKind);
    }
}
