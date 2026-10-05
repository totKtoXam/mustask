using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// Builds M1's structure for a test without any endpoint, so no lane depends on
/// another (mod-002 plan-backend § 14.1 step 5): through the domain factories,
/// the repositories and mod-001's real seed seam, each step in a unit of work
/// of its own, from the shared host's services. The integration database is
/// shared by the whole collection and never emptied, and organization names are
/// unique instance-wide, so every organization gets a unique name unless the
/// test passes one.
/// </summary>
public static class OrganizationsTestData
{
    public const string Creator = "1c9e2b7d-0000-4000-8000-00000000c0de";

    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A name no other test uses: the prefix and 32 hex digits.</summary>
    public static string UniqueName(string prefix = "Организация") => $"{prefix} {Guid.CreateVersion7():N}";

    public static EntityName Name(string value) =>
        EntityName.TryCreate(value, out var name) ? name : throw new ArgumentException($"«{value}» is not a valid name.", nameof(value));

    /// <summary>An organization with its seeded process, active or deactivated.</summary>
    public static Task<Organization> CreateOrganizationAsync(
        MustaskApiFactory factory, string? name = null, OrganizationStatus status = OrganizationStatus.Active) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var organizationId = Guid.CreateVersion7();
            var processId = await services.GetRequiredService<IProcessSeed>().SeedAsync(organizationId, ct);
            var organization = Organization.Create(organizationId, Name(name ?? UniqueName()), processId, Now, Creator);

            if (status == OrganizationStatus.Deactivated)
            {
                organization.Deactivate();
            }

            var organizations = services.GetRequiredService<IOrganizationRepository>();
            organizations.Add(organization);
            await organizations.SaveAsync(ct);

            return organization;
        });

    /// <summary>A project with its first team and its root (US-3), active or deactivated.</summary>
    public static Task<NewProject> CreateProjectAsync(
        MustaskApiFactory factory,
        Guid organizationId,
        string name = "Пример проекта",
        string key = "PRM",
        ProjectStatus status = ProjectStatus.Active) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var project = Project.Create(organizationId, Name(name), key, Now, Creator);

            if (status == ProjectStatus.Deactivated)
            {
                project.Project.Deactivate();
            }

            var projects = services.GetRequiredService<IProjectRepository>();
            projects.Add(project);
            await projects.SaveAsync(ct);

            return project;
        });

    /// <summary>An active organization with one project «Пример проекта» (PRM) and a second team «Платформа» with its area.</summary>
    public static async Task<OrganizationStructure> CreateStructureAsync(MustaskApiFactory factory, string? organizationName = null)
    {
        var organization = await CreateOrganizationAsync(factory, organizationName);
        var project = await CreateProjectAsync(factory, organization.Id);
        var platform = await AddTeamAsync(factory, project.Project.Id, "Платформа");

        return new OrganizationStructure(organization, project, platform);
    }

    /// <summary>A second or later team with its own area under the root (US-4).</summary>
    public static Task<NewTeam> AddTeamAsync(MustaskApiFactory factory, Guid projectId, string name) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var root = await services.GetRequiredService<IAreaRepository>().LockRootAsync(projectId, forUpdate: false, ct);
            var team = Team.CreateWithArea(projectId, Name(name), root);
            var teams = services.GetRequiredService<ITeamRepository>();
            teams.Add(team);
            await teams.SaveAsync(ct);

            return team;
        });

    /// <summary>A child area, owned explicitly by <paramref name="ownerTeamId"/> or inheriting (US-5, US-6).</summary>
    public static Task<Area> AddAreaAsync(MustaskApiFactory factory, Guid projectId, Guid parentId, string name, Guid? ownerTeamId = null) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var owner = ownerTeamId is { } teamId
                ? await services.GetRequiredService<ITeamRepository>().LockForShareAsync(projectId, teamId, ct)
                : null;
            var areas = services.GetRequiredService<IAreaRepository>();
            var parent = await areas.LockForShareAsync(projectId, parentId, ct)
                ?? throw new ArgumentException("The parent is not an area of the project.", nameof(parentId));
            var area = Area.CreateChild(parent, Name(name), owner);
            areas.Add(area);
            await areas.SaveAsync(ct);

            return area;
        });

    /// <summary>Deletes a team as US-7 does: its explicit areas pass to the first team, then the team is deleted.</summary>
    public static Task DeleteTeamAsync(MustaskApiFactory factory, Guid projectId, Guid teamId) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var teams = services.GetRequiredService<ITeamRepository>();
            var team = await teams.LockForUpdateAsync(projectId, teamId, ct)
                ?? throw new ArgumentException("The team is not a team of the project.", nameof(teamId));
            var first = await teams.GetFirstTeamAsync(projectId, ct);

            await teams.ReassignExplicitAreasAsync(team.Id, first.Id, ct);
            team.Delete();
            await teams.SaveAsync(ct);

            return true;
        });

    /// <summary>
    /// Makes <paramref name="areaId"/> the team's default area as US-4 does, without the endpoint, so a test needs no other
    /// use case: the team <c>FOR NO KEY UPDATE</c>, then the current default and the new area <c>FOR UPDATE</c> in one
    /// statement, the domain rule, and the old mark cleared and flushed before the new one is set, because the default
    /// index is checked row by row (plan-backend § 8.2). Answers whether anything changed; the domain's refusal is thrown.
    /// </summary>
    public static Task<bool> SetTeamDefaultAreaAsync(MustaskApiFactory factory, Guid projectId, Guid teamId, Guid areaId) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var teams = services.GetRequiredService<ITeamRepository>();
            var areas = services.GetRequiredService<IAreaRepository>();
            var team = await teams.LockForNoKeyUpdateAsync(projectId, teamId, ct)
                ?? throw new ArgumentException("The team is not a team of the project.", nameof(teamId));
            var currentId = (await areas.GetNodesAsync(projectId, ct)).Single(node => node.DefaultOfTeamId == team.Id).Id;
            var locked = await areas.LockManyForUpdateAsync(projectId, [currentId, areaId], ct);
            var current = locked.Single(area => area.Id == currentId);
            var next = locked.SingleOrDefault(area => area.Id == areaId)
                ?? throw new ArgumentException("The area is not an area of the project.", nameof(areaId));

            if (!TeamDefaultArea.RequiresChange(team, current, next))
            {
                return false;
            }

            current.ClearDefault();
            await areas.SaveAsync(ct);
            next.MarkDefaultOf(team);
            await areas.SaveAsync(ct);

            return true;
        });

    /// <summary>Deactivates or restores an organization (US-2).</summary>
    public static Task SetOrganizationStatusAsync(MustaskApiFactory factory, Guid organizationId, OrganizationStatus status) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var organizations = services.GetRequiredService<IOrganizationRepository>();
            var organization = await organizations.LockAsync(organizationId, ct)
                ?? throw new ArgumentException("No such organization.", nameof(organizationId));

            if (status == OrganizationStatus.Deactivated)
            {
                organization.Deactivate();
            }
            else
            {
                organization.Restore();
            }

            await organizations.SaveAsync(ct);

            return true;
        });

    /// <summary>Deactivates or restores a project (US-3).</summary>
    public static Task SetProjectStatusAsync(MustaskApiFactory factory, Guid organizationId, Guid projectId, ProjectStatus status) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var project = await projects.LockAsync(organizationId, projectId, ct)
                ?? throw new ArgumentException("No such project in the organization.", nameof(projectId));

            if (status == ProjectStatus.Deactivated)
            {
                project.Deactivate();
            }
            else
            {
                project.Restore();
            }

            await projects.SaveAsync(ct);

            return true;
        });

    /// <summary>The project's areas as stored now, whatever the project's status.</summary>
    public static async Task<AreaTree> ReadTreeAsync(MustaskApiFactory factory, Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        var nodes = await scope.ServiceProvider.GetRequiredService<IAreaRepository>().GetNodesAsync(projectId, CancellationToken.None);

        return new AreaTree(nodes);
    }

    /// <summary>Runs <paramref name="work"/> in a unit of work of its own, in a scope of the shared host.</summary>
    public static async Task<T> InUnitOfWorkAsync<T>(MustaskApiFactory factory, Func<IServiceProvider, CancellationToken, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return await unitOfWork.ExecuteInTransactionAsync(ct => work(scope.ServiceProvider, ct), CancellationToken.None);
    }
}

/// <summary>What <see cref="OrganizationsTestData.CreateStructureAsync"/> wrote.</summary>
public sealed record OrganizationStructure(Organization Organization, NewProject Project, NewTeam Platform)
{
    public Guid OrganizationId => Organization.Id;

    public Guid ProjectId => Project.Project.Id;

    public Guid RootId => Project.Root.Id;

    public Guid FirstTeamId => Project.FirstTeam.Id;
}
