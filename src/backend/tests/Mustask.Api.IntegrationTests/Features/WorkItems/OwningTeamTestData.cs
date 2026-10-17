using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// The owning team's tests' data (mod-017): the organization of the stories — «Альфа» (ALP) with the teams MARS and VENUS beside
/// its own, «Бета» (BET) with JUPITER, a deactivated project with a team, a deleted team, and another organization with SATURN.
/// Built without any endpoint, as <see cref="OrganizationsTestData"/> builds its structure, and from the helpers that stay after
/// the line: a plain team (<see cref="Team.Create"/>), the owning team set with SQL, a place written with SQL.
/// </summary>
internal static class OwningTeamTestData
{
    /// <summary>The world of the stories.</summary>
    /// <param name="Items">The organization, «Альфа» (ALP), its root and the process' types.</param>
    /// <param name="OwnTeam">The team «Альфа» is created with.</param>
    /// <param name="OtherProjectId">«Бета» (BET), a project of the same organization.</param>
    /// <param name="OtherProjectTeam">JUPITER, a team of «Бета».</param>
    /// <param name="DeactivatedProjectTeam">The team of a deactivated project of the organization.</param>
    /// <param name="DeletedTeam">A team of «Альфа» that was deleted.</param>
    /// <param name="OtherOrganizationId">«Другая», an organization that is not the caller's.</param>
    /// <param name="OtherOrganizationTeam">SATURN, a team of «Другая».</param>
    internal sealed record OwningTeamWorld(
        WorkItemsWorld Items,
        Guid OwnTeam,
        Guid Mars,
        Guid Venus,
        Guid OtherProjectId,
        Guid OtherProjectTeam,
        Guid DeactivatedProjectTeam,
        Guid DeletedTeam,
        Guid OtherOrganizationId,
        Guid OtherOrganizationTeam)
    {
        public Guid OrganizationId => Items.OrganizationId;

        public Guid ProjectId => Items.ProjectId;
    }

    public static async Task<OwningTeamWorld> CreateWorldAsync(MustaskApiFactory factory)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var alpha = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Альфа", "ALP");
        var types = await WorkItemsTestData.TypesAsync(factory, organization.Id);
        var items = new WorkItemsWorld(organization.Id, alpha.Project.Id, alpha.Root.Id, "ALP", types);

        var mars = await AddTeamAsync(factory, alpha.Project.Id, "MARS");
        var venus = await AddTeamAsync(factory, alpha.Project.Id, "VENUS");
        var deleted = await AddTeamAsync(factory, alpha.Project.Id, "URANUS");
        await OrganizationsTestData.DeleteTeamAsync(factory, alpha.Project.Id, deleted);

        var beta = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Бета", "BET");
        var jupiter = await AddTeamAsync(factory, beta.Project.Id, "JUPITER");

        var sleeping = await OrganizationsTestData.CreateProjectAsync(
            factory, organization.Id, "Спящий", "SLP", ProjectStatus.Deactivated);
        var sleepingTeam = await AddTeamAsync(factory, sleeping.Project.Id, "NEPTUNE");

        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var otherProject = await OrganizationsTestData.CreateProjectAsync(factory, other.Id, "Чужой", "OTH");
        var saturn = await AddTeamAsync(factory, otherProject.Project.Id, "SATURN");

        return new OwningTeamWorld(items, alpha.FirstTeam.Id, mars, venus, beta.Project.Id, jupiter, sleepingTeam, deleted, other.Id, saturn);
    }

    /// <summary>
    /// The world as the boards' helpers know it, so a test of the views uses <see cref="BoardsTestData"/> and
    /// <see cref="ProjectBoardsTestData"/> (rows, boards, reorders) over the world of the stories: «Альфа»'s own team stands for
    /// the first team, MARS for the second. The area ids are empty — the views read none.
    /// </summary>
    public static BoardsWorld BoardsViewOf(OwningTeamWorld world) =>
        new(world.Items, world.OwnTeam, world.Mars, Guid.Empty, Guid.Empty, Guid.Empty);

    /// <summary>An ordinary team of the project, with no area (BR-OT-11).</summary>
    public static Task<Guid> AddTeamAsync(MustaskApiFactory factory, Guid projectId, string name) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var team = Team.Create(projectId, OrganizationsTestData.Name(name));
            var teams = services.GetRequiredService<Mustask.Application.Abstractions.Organizations.ITeamRepository>();
            teams.Add(team);
            await teams.SaveAsync(ct);

            return team.Id;
        });

    /// <summary>The item's owning team set with SQL, for a test that needs the item placed without a save or a creation.</summary>
    public static Task SetOwningTeamAsync(MustaskApiFactory factory, Guid workItemId, Guid? teamId) =>
        WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET owning_team_id = {teamId} WHERE id = {workItemId}");

    /// <summary>The owning team and «Изменён» of an item as stored, by its number.</summary>
    public static async Task<StoredTeam> StoredAsync(MustaskApiFactory factory, Guid projectId, string identifier)
    {
        var number = int.Parse(identifier[(identifier.LastIndexOf('-') + 1)..]);
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Number == number)
            .Select(item => new StoredTeam(item.Id, item.OwningTeamId, item.ChangedAt, item.AreaId, item.AssigneeId, item.StateId))
            .SingleAsync();
    }

    /// <summary>The project default team as the structure seam resolves it now.</summary>
    public static async Task<Guid?> DefaultTeamAsync(MustaskApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var project = await scope.ServiceProvider.GetRequiredService<Mustask.Application.Features.Organizations.IOrganizationStructure>()
            .FindProjectAsync(projectId, CancellationToken.None);

        return project!.DefaultTeamId;
    }

    /// <summary>A team place written with SQL: the backlog's order row, then the place (the item must be owned by the team).</summary>
    public static async Task PlaceAsync(MustaskApiFactory factory, Guid workItemId, Guid teamId, Guid levelId, int position)
    {
        await WorkItemsTestData.ExecuteAsync(
            factory,
            $"INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES ({teamId}, {levelId}) ON CONFLICT (team_id, backlog_level_id) DO NOTHING");
        await WorkItemsTestData.ExecuteAsync(
            factory,
            $"""
             INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
             VALUES ({workItemId}, {teamId}, {levelId}, {position})
             ON CONFLICT (work_item_id) DO UPDATE SET team_id = excluded.team_id, backlog_level_id = excluded.backlog_level_id, position = excluded.position
             """);
    }

    /// <summary>The team place rows of an item (zero or one), straight from the table.</summary>
    public static async Task<int> TeamPlacesOfAsync(MustaskApiFactory factory, Guid workItemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM backlog_places WHERE work_item_id = {workItemId}")
            .SingleAsync();
    }

    /// <summary>The team places of the project's items that name a team other than the owner of their item (ADR-0028 rule 5): always zero.</summary>
    public static async Task<int> PlacesNamingAnotherTeamAsync(MustaskApiFactory factory, Guid projectId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database
            .SqlQuery<int>(
                $"""
                 SELECT count(*)::int AS "Value"
                 FROM backlog_places AS place JOIN work_items AS item ON item.id = place.work_item_id
                 WHERE item.project_id = {projectId} AND place.team_id IS DISTINCT FROM item.owning_team_id
                 """)
            .SingleAsync();
    }

    /// <summary>The project place rows of an item (zero or one), straight from the table.</summary>
    public static async Task<int> ProjectPlacesOfAsync(MustaskApiFactory factory, Guid workItemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM project_backlog_places WHERE work_item_id = {workItemId}")
            .SingleAsync();
    }

    /// <summary>The <c>team</c> of an item answer: its id and name, or <c>null</c> for «Без команды». Fails the test if the field is missing.</summary>
    public static (Guid Id, string Name)? TeamOf(JsonElement item)
    {
        Assert.True(item.TryGetProperty("team", out var team), "The item answer has no 'team' field.");

        return team.ValueKind == JsonValueKind.Null
            ? null
            : (team.GetProperty("id").GetGuid(), team.GetProperty("name").GetString()!);
    }

    /// <summary>A created item's id and identifier, from the creation's answer.</summary>
    public static async Task<(Guid Id, string Identifier)> CreateAsync(MustaskApiFactory factory, WorkItemsWorld world, string typeKey, string title)
    {
        var answer = await WorkItemsTestData.CreateAsync(factory, world, typeKey, title);

        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return (answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }
}

/// <summary>An item's owning team and the fields a team change must leave alone, as stored.</summary>
internal sealed record StoredTeam(Guid Id, Guid? TeamId, DateTimeOffset ChangedAt, Guid AreaId, string? AssigneeId, Guid StateId);
