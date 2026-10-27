using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
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
    /// A project with its root and — as most arrangements of the other modules' tests need — <b>one team of the organization named
    /// after it, linked to it</b> (feat-028: a project no longer comes with a team; a team is the organization's and a link joins it
    /// to the project, so the arrangement builds both). Active or deactivated. Because the team stands for "the project's team" in
    /// the tests written before the move, the project's creator (<see cref="Creator"/>) is that team's only member when they are a
    /// member of the organization; a test that wants the team empty passes <c>creatorJoinsFirstTeam: false</c>. A test about a project
    /// with <b>no</b> team builds it with <see cref="CreateBareProjectAsync"/>.
    /// </summary>
    public static Task<ProjectWithTeam> CreateProjectAsync(
        MustaskApiFactory factory,
        Guid organizationId,
        string name = "Пример проекта",
        string key = "PRM",
        ProjectStatus status = ProjectStatus.Active,
        bool creatorJoinsFirstTeam = true) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var project = Project.Create(organizationId, Name(name), key, Now, Creator);
            var team = Team.Create(organizationId, Name(name));

            if (status == ProjectStatus.Deactivated)
            {
                project.Deactivate();
            }

            var projects = services.GetRequiredService<IProjectRepository>();
            projects.Add(project);
            services.GetRequiredService<ITeamRepository>().Add(team);
            services.GetRequiredService<IProjectTeamRepository>().Add(ProjectTeam.Link(organizationId, project.Id, team.Id, Creator, Now));

            if (creatorJoinsFirstTeam && await services.GetRequiredService<IMembershipRepository>().ExistsAsync(organizationId, Creator, ct))
            {
                services.GetRequiredService<ITeamMembershipRepository>().Add(TeamMembership.Add(team.Id, organizationId, Creator));
            }

            await projects.SaveAsync(ct);

            return new ProjectWithTeam(project, team);
        });

    /// <summary>A project with its root and <b>no team</b> (feat-028 BR-OL-2), active or deactivated.</summary>
    public static Task<Project> CreateBareProjectAsync(
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
                project.Deactivate();
            }

            var projects = services.GetRequiredService<IProjectRepository>();
            projects.Add(project);
            await projects.SaveAsync(ct);

            return project;
        });

    /// <summary>An active organization with one project «Пример проекта» (PRM), its team of the same name and a second linked team «Платформа».</summary>
    public static async Task<OrganizationStructure> CreateStructureAsync(
        MustaskApiFactory factory, string? organizationName = null, bool founded = true, bool creatorJoinsFirstTeam = true)
    {
        var organization = await CreateOrganizationAsync(factory, organizationName, founded: founded);
        var project = await CreateProjectAsync(factory, organization.Id, creatorJoinsFirstTeam: creatorJoinsFirstTeam);
        var platform = await AddTeamAsync(factory, project.Project.Id, "Платформа");

        return new OrganizationStructure(organization, project, platform);
    }

    /// <summary>
    /// A team of the project's organization, <b>linked to the project</b> (feat-028): the helper the tests written before the move
    /// call "a team of the project". It is ordinary: no area, no first-team role (mod-017 BR-OT-10, BR-OT-11).
    /// </summary>
    public static Task<Team> AddTeamAsync(MustaskApiFactory factory, Guid projectId, string name) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var organizationId = await OrganizationOfAsync(services, projectId, ct);
            var team = Team.Create(organizationId, Name(name));
            var teams = services.GetRequiredService<ITeamRepository>();
            teams.Add(team);
            services.GetRequiredService<IProjectTeamRepository>().Add(ProjectTeam.Link(organizationId, projectId, team.Id, Creator, Now));
            await teams.SaveAsync(ct);

            return team;
        });

    /// <summary>A team of the organization that is linked to no project (feat-028 BR-OL-1).</summary>
    public static Task<Team> AddUnlinkedTeamAsync(MustaskApiFactory factory, Guid organizationId, string name) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var team = Team.Create(organizationId, Name(name));
            var teams = services.GetRequiredService<ITeamRepository>();
            teams.Add(team);
            await teams.SaveAsync(ct);

            return team;
        });

    /// <summary>Links a team to a project, recording <see cref="Creator"/> and <see cref="Now"/> (feat-028 BR-OL-4).</summary>
    public static Task LinkAsync(MustaskApiFactory factory, Guid projectId, Guid teamId) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var organizationId = await OrganizationOfAsync(services, projectId, ct);
            var links = services.GetRequiredService<IProjectTeamRepository>();
            links.Add(ProjectTeam.Link(organizationId, projectId, teamId, Creator, Now));
            await links.SaveAsync(ct);

            return true;
        });

    /// <summary>The id of the organization a project belongs to.</summary>
    public static async Task<Guid> OrganizationOfAsync(MustaskApiFactory factory, Guid projectId)
    {
        using var scope = factory.Services.CreateScope();

        return await OrganizationOfAsync(scope.ServiceProvider, projectId, CancellationToken.None);
    }

    private static Task<Guid> OrganizationOfAsync(IServiceProvider services, Guid projectId, CancellationToken cancellationToken) =>
        services.GetRequiredService<Mustask.Infrastructure.MustaskDbContext>().Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.OrganizationId)
            .SingleAsync(cancellationToken);

    /// <summary>An area of the organization under <paramref name="parentId"/> (null: the top level), written the way the use case writes it.</summary>
    public static Task<Area> AddAreaAsync(MustaskApiFactory factory, Guid organizationId, Guid? parentId, string name) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var areas = services.GetRequiredService<IAreaRepository>();
            await areas.LockTreeAsync(organizationId, ct);
            var area = Area.Create(organizationId, parentId, Name(name), null, Creator, Now);
            areas.Add(area);
            await areas.SaveAsync(ct);

            return area;
        });

    /// <summary>Links an area to a project, as the link writes it (mod-018 BR-AW-8); a second link of the pair is a no-op.</summary>
    public static Task LinkAreaToProjectAsync(MustaskApiFactory factory, Guid organizationId, Guid areaId, Guid projectId) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var links = services.GetRequiredService<IAreaLinkRepository>();
            await links.LinkProjectsAsync(organizationId, [(areaId, projectId)], Creator, Now, ct);

            return true;
        });

    /// <summary>Links an area to a team, as the link writes it (mod-018 BR-AW-8).</summary>
    public static Task LinkAreaToTeamAsync(MustaskApiFactory factory, Guid organizationId, Guid areaId, Guid teamId) =>
        InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var links = services.GetRequiredService<IAreaLinkRepository>();
            await links.LinkTeamsAsync(organizationId, [(areaId, teamId)], Creator, Now, ct);

            return true;
        });

    /// <summary>
    /// Deletes a team as US-5 does (feat-028 BR-OL-9): through the use case itself, in its own unit of work — the items the team
    /// owns in every project it is linked to go to «Без команды», its places and the settings that named it go, its links and
    /// memberships end. The project is only the way to find the organization.
    /// </summary>
    public static async Task DeleteTeamAsync(MustaskApiFactory factory, Guid projectId, Guid teamId)
    {
        var organizationId = await OrganizationOfAsync(factory, projectId);

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<Mustask.Application.Features.Organizations.DeleteTeam.DeleteTeamHandler>()
            .Handle(
                new Mustask.Application.Features.Organizations.DeleteTeam.DeleteTeamCommand(organizationId.ToString(), teamId.ToString()),
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
                _ = await services.GetRequiredService<IProjectTeamRepository>().LockLinkForShareAsync(organizationId, projectId, id, ct);
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

    /// <summary>The organization's areas as stored now.</summary>
    public static async Task<AreaTree> ReadTreeAsync(MustaskApiFactory factory, Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();
        var nodes = await scope.ServiceProvider.GetRequiredService<IAreaRepository>().GetNodesAsync(organizationId, CancellationToken.None);

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
public sealed record OrganizationStructure(Organization Organization, ProjectWithTeam Project, Team Platform)
{
    public Guid OrganizationId => Organization.Id;

    public Guid ProjectId => Project.Project.Id;


    /// <summary>The team the project was created with: an ordinary team (mod-017 BR-OT-11).</summary>
    public Guid ProjectTeamId => Project.Team.Id;
}

/// <summary>A project, its root and the team of the organization built with it and linked to it (<see cref="OrganizationsTestData.CreateProjectAsync"/>).</summary>
public sealed record ProjectWithTeam(Project Project, Team Team);
