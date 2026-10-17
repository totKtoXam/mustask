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

    /// <summary>
    /// An organization with its seeded process, active or deactivated. By default it is <paramref name="founded"/> as the
    /// create handler founds one (mod-016 BR-M1-25): <see cref="Creator"/> is its only member, an <c>admin</c> added by
    /// themselves, so a test that signs in with <see cref="OrganizationsTokens.SignedIn"/> administers the organization it
    /// built. A test that needs an organization nobody belongs to passes <c>founded: false</c>.
    /// </summary>
    public static Task<Organization> CreateOrganizationAsync(
        MustaskApiFactory factory, string? name = null, OrganizationStatus status = OrganizationStatus.Active, bool founded = true) =>
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

            if (founded)
            {
                services.GetRequiredService<IMembershipRepository>().Add(OrganizationMembership.Founder(organizationId, Creator, Now));
            }

            await organizations.SaveAsync(ct);

            return organization;
        });

    /// <summary>
    /// A project with its team and its root (US-3), active or deactivated. As the create handler does (BR-M1-25), the
    /// project's creator (<see cref="Creator"/>) is that team's only member when they are a member of the organization;
    /// a test that wants the team empty passes <c>creatorJoinsFirstTeam: false</c>.
    /// </summary>
    public static Task<NewProject> CreateProjectAsync(
        MustaskApiFactory factory,
        Guid organizationId,
        string name = "Пример проекта",
        string key = "PRM",
        ProjectStatus status = ProjectStatus.Active,
        bool creatorJoinsFirstTeam = true) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var project = Project.Create(organizationId, Name(name), key, Now, Creator);

            if (status == ProjectStatus.Deactivated)
            {
                project.Project.Deactivate();
            }

            var projects = services.GetRequiredService<IProjectRepository>();
            projects.Add(project);

            if (creatorJoinsFirstTeam && await services.GetRequiredService<IMembershipRepository>().ExistsAsync(organizationId, Creator, ct))
            {
                services.GetRequiredService<ITeamMembershipRepository>()
                    .Add(TeamMembership.Add(project.Team.Id, organizationId, Creator));
            }

            await projects.SaveAsync(ct);

            return project;
        });

    /// <summary>An active organization with one project «Пример проекта» (PRM) and a second team «Платформа».</summary>
    public static async Task<OrganizationStructure> CreateStructureAsync(
        MustaskApiFactory factory, string? organizationName = null, bool founded = true, bool creatorJoinsFirstTeam = true)
    {
        var organization = await CreateOrganizationAsync(factory, organizationName, founded: founded);
        var project = await CreateProjectAsync(factory, organization.Id, creatorJoinsFirstTeam: creatorJoinsFirstTeam);
        var platform = await AddTeamAsync(factory, project.Project.Id, "Платформа");

        return new OrganizationStructure(organization, project, platform);
    }

    /// <summary>A second or later team (US-4): an ordinary one, with no area (mod-017 BR-OT-10).</summary>
    public static Task<Team> AddTeamAsync(MustaskApiFactory factory, Guid projectId, string name) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var team = Team.Create(projectId, Name(name));
            var teams = services.GetRequiredService<ITeamRepository>();
            teams.Add(team);
            await teams.SaveAsync(ct);

            return team;
        });

    /// <summary>A child area (US-5): it owns nothing and has no team (mod-017 BR-OT-10).</summary>
    public static Task<Area> AddAreaAsync(MustaskApiFactory factory, Guid projectId, Guid parentId, string name) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var areas = services.GetRequiredService<IAreaRepository>();
            var parent = await areas.LockForShareAsync(projectId, parentId, ct)
                ?? throw new ArgumentException("The parent is not an area of the project.", nameof(parentId));
            var area = Area.CreateChild(parent, Name(name));
            areas.Add(area);
            await areas.SaveAsync(ct);

            return area;
        });

    /// <summary>
    /// Deletes a team as US-7 does (mod-017 BR-OT-12): through the use case itself, in its own unit of work — the items the team
    /// owns go to «Без команды», its places and a default setting that named it go, its memberships end. The project must have
    /// another team (BR-OT-11).
    /// </summary>
    public static async Task DeleteTeamAsync(MustaskApiFactory factory, Guid projectId, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var project = await scope.ServiceProvider.GetRequiredService<Mustask.Application.Features.Organizations.IOrganizationStructure>()
            .FindProjectAsync(projectId, CancellationToken.None)
            ?? throw new ArgumentException("No such project.", nameof(projectId));

        await scope.ServiceProvider
            .GetRequiredService<Mustask.Application.Features.Organizations.DeleteTeam.DeleteTeamHandler>()
            .Handle(
                new Mustask.Application.Features.Organizations.DeleteTeam.DeleteTeamCommand(
                    project.OrganizationId.ToString(), projectId.ToString(), teamId.ToString()),
                CancellationToken.None);
    }

    /// <summary>
    /// Names the project's default team (or clears the setting with <c>null</c>) as <c>PUT …/default-team</c> does, without the
    /// endpoint (mod-017 BR-OT-4): the team <c>FOR SHARE</c>, the project row after it, the domain's write.
    /// </summary>
    public static Task SetDefaultTeamAsync(MustaskApiFactory factory, Guid organizationId, Guid projectId, Guid? teamId) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            if (teamId is { } id)
            {
                _ = await services.GetRequiredService<ITeamRepository>().LockForShareAsync(projectId, id, ct);
            }

            var projects = services.GetRequiredService<IProjectRepository>();
            var project = await projects.LockAsync(organizationId, projectId, ct)
                ?? throw new ArgumentException("No such project in the organization.", nameof(projectId));

            project.NameDefaultTeam(teamId);
            await projects.SaveAsync(ct);

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
public sealed record OrganizationStructure(Organization Organization, NewProject Project, Team Platform)
{
    public Guid OrganizationId => Organization.Id;

    public Guid ProjectId => Project.Project.Id;

    public Guid RootId => Project.Root.Id;

    /// <summary>The team the project was created with: an ordinary team (mod-017 BR-OT-11).</summary>
    public Guid ProjectTeamId => Project.Team.Id;
}
