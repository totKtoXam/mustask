using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Identity;
using Mustask.Domain.Process;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// The work items tests' data and requests (mod-003). Every test builds its own
/// organization with mod-002's <see cref="OrganizationsTestData"/> — the
/// database is shared by the collection and never emptied — and creates its
/// items through the create endpoint, so what a test reads is what the API
/// wrote. Users are put into mod-014's projection under fresh ids.
/// </summary>
internal static class WorkItemsTestData
{
    /// <summary>An active organization «Пример организации …» with a project «Мастаск» keyed MSK and its root «Мастаск».</summary>
    public static async Task<WorkItemsWorld> CreateWorldAsync(MustaskApiFactory factory, string key = "MSK", string projectName = "Мастаск")
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, projectName, key);
        var types = await TypesAsync(factory, organization.Id);

        return new WorkItemsWorld(organization.Id, project.Project.Id, project.Root.Id, key, types);
    }

    /// <summary>Another project of the same organization, with its own root.</summary>
    public static async Task<WorkItemsWorld> AddProjectAsync(MustaskApiFactory factory, WorkItemsWorld world, string key, string name)
    {
        var project = await OrganizationsTestData.CreateProjectAsync(factory, world.OrganizationId, name, key);

        return world with { ProjectId = project.Project.Id, RootAreaId = project.Root.Id, Key = key };
    }

    /// <summary>The organization's five types by key, each with its states by name, as mod-001 seeded them.</summary>
    public static async Task<IReadOnlyDictionary<string, WorkItemTypeRow>> TypesAsync(MustaskApiFactory factory, Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var process = await scope.ServiceProvider.GetRequiredService<IProcessDefinitionReadModel>()
            .FindByOrganizationAsync(organizationId, CancellationToken.None);

        return process!.WorkItemTypes.ToDictionary(type => type.Key, StringComparer.Ordinal);
    }

    public static string List(WorkItemsWorld world) =>
        $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}/workitems";

    public static string Item(WorkItemsWorld world, string identifier) => $"{List(world)}/{identifier}";

    public static string Candidates(Guid organizationId, string? text) =>
        text is null
            ? $"/api/organizations/{organizationId}/workitems/assignee-candidates"
            : $"/api/organizations/{organizationId}/workitems/assignee-candidates?text={Uri.EscapeDataString(text)}";

    /// <summary>The create request as the dialog sends it, under a fresh submission key unless the test names one.</summary>
    public static Task<Answer> CreateAsync(
        MustaskApiFactory factory,
        WorkItemsWorld world,
        string typeKey,
        string title,
        Guid? requestId = null,
        string? token = null,
        CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            List(world),
            new { requestId = (requestId ?? Guid.NewGuid()).ToString(), typeId = world.Types[typeKey].Id.ToString(), title },
            token ?? OrganizationsTokens.SignedIn(),
            gate);

    /// <summary>A created item's identifier; fails the test when the creation did not answer 201.</summary>
    public static async Task<string> CreateItemAsync(MustaskApiFactory factory, WorkItemsWorld world, string typeKey, string title)
    {
        var answer = await CreateAsync(factory, world, typeKey, title);

        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return answer.Body.GetProperty("identifier").GetString()!;
    }

    public static Task<Answer> GetAsync(MustaskApiFactory factory, WorkItemsWorld world, string identifier) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Item(world, identifier), token: OrganizationsTokens.SignedIn());

    public static Task<Answer> ListAsync(MustaskApiFactory factory, WorkItemsWorld world, string? query = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, List(world) + (query ?? string.Empty), token: OrganizationsTokens.SignedIn());

    /// <summary>A save as the form sends it: an anonymous object holding only the changed fields.</summary>
    public static Task<Answer> SaveAsync(MustaskApiFactory factory, WorkItemsWorld world, string identifier, object body, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Patch, Item(world, identifier), body, OrganizationsTokens.SignedIn(), gate);

    /// <summary>The item as the read endpoint answers it; fails the test when it does not answer 200.</summary>
    public static async Task<JsonElement> ReadAsync(MustaskApiFactory factory, WorkItemsWorld world, string identifier)
    {
        var answer = await GetAsync(factory, world, identifier);

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body;
    }

    /// <summary>A user of the directory under a fresh id: active unless the test says otherwise.</summary>
    public static async Task<string> AddUserAsync(MustaskApiFactory factory, string firstName, string lastName, string? email = null, bool active = true)
    {
        var id = UserDirectoryTestData.NewId();
        await UserDirectoryTestData.SeedAsync(factory, [UserDirectoryTestData.Push(id, firstName, lastName, email, enabled: active)]);

        return id;
    }

    /// <summary>
    /// A user of the directory under a fresh id who is also a member of the world's organization (mod-016 BR-M1-26: only an
    /// active member can be made an assignee), in any role; active unless the test says otherwise.
    /// </summary>
    public static async Task<string> AddMemberAsync(
        MustaskApiFactory factory,
        WorkItemsWorld world,
        string firstName,
        string lastName,
        string? email = null,
        bool active = true,
        OrganizationRole role = OrganizationRole.Member)
    {
        var id = await AddUserAsync(factory, firstName, lastName, email, active);
        await MembershipTestData.AddMemberAsync(factory, world.OrganizationId, id, role);

        return id;
    }

    /// <summary>Runs SQL against the test database, for what no endpoint of this task can do (a user disabled, a time set).</summary>
    public static async Task ExecuteAsync(MustaskApiFactory factory, FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database.ExecuteSqlInterpolatedAsync(sql);
    }

    /// <summary>The stored items of a project, read straight from the table.</summary>
    public static async Task<List<StoredItem>> StoredAsync(MustaskApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderBy(item => item.Number)
            .Select(item => new StoredItem(item.Number, item.Title, item.Description, item.StateId, item.AssigneeId, item.AreaId, item.ChangedAt))
            .ToListAsync();
    }

    public static Guid StateId(this WorkItemTypeRow type, string name) => type.States.Single(state => state.Name == name).Id;

    public static Guid StateId(this WorkItemTypeRow type, StateCategory category) => type.States.Single(state => state.Category == category).Id;
}

/// <summary>An organization, one of its projects with its root, and the organization's types by key.</summary>
internal sealed record WorkItemsWorld(
    Guid OrganizationId,
    Guid ProjectId,
    Guid RootAreaId,
    string Key,
    IReadOnlyDictionary<string, WorkItemTypeRow> Types);

/// <summary>An item as the table holds it.</summary>
internal sealed record StoredItem(int Number, string Title, string Description, Guid StateId, string? AssigneeId, Guid AreaId, DateTimeOffset ChangedAt);
