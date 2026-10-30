using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Process;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Mustask.Testing;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// One database for the organizations tests of this assembly, migrated with
/// the real migrations — a copy of the migrated template on the assembly's one
/// server (tech-047); every class fixture gets a copy of its own. Like the process fixture, it gives
/// every context its connection string explicitly and never touches the
/// process-wide <c>ConnectionStrings__mustask</c>. The database is shared by
/// the collection and never emptied, so every test names its organizations
/// uniquely (<see cref="UniqueName"/>).
/// </summary>
public sealed class OrganizationsDatabaseFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public const string Creator = "8a2f0c1e-0000-4000-8000-00000000c0de";

    public string ConnectionString { get; private set; } = "";

    // A migrated database of its own on the assembly's one server (tech-047).
    public async Task InitializeAsync() =>
        ConnectionString = await SharedPostgres.CreateDatabaseFromTemplateAsync(MigratedDatabase.Template);

    public Task DisposeAsync() => Task.CompletedTask;

    public static string UniqueName(string prefix) => $"{prefix} {Guid.CreateVersion7():N}";

    public static EntityName Name(string value) =>
        EntityName.TryCreate(value, out var name) ? name : throw new ArgumentException($"«{value}» is not a valid name.", nameof(value));

    /// <summary>A context of its own, for reading back what a test wrote.</summary>
    public MustaskDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(ConnectionString);

        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new MustaskDbContext(options.Options);
    }

    /// <summary>
    /// The module as production wires it, minus Aspire: a scoped context, the
    /// unit of work, mod-001's seed, and this module's repositories, read models
    /// and seam.
    /// </summary>
    public ServiceProvider BuildServices(params IInterceptor[] interceptors) => BuildServices(ConnectionString, interceptors);

    /// <summary>The same wiring over another database of the same server (a migration test that gives each test a database of its own).</summary>
    public ServiceProvider BuildServices(string connectionString, params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options =>
        {
            options.UseNpgsql(connectionString);

            if (interceptors.Length > 0)
            {
                options.AddInterceptors(interceptors);
            }
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProcessSeedStore, ProcessSeedStore>();
        services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Puts users into the user projection (mod-014) the way its push does, so the member reads (mod-016) have people to name.
    /// The projection is shared by the collection and never emptied, so a test names its users by ids no other test uses.
    /// </summary>
    public async Task SeedUsersAsync(params (string Id, string FirstName, string LastName, string Email, bool Enabled)[] users)
    {
        await using var context = NewContext();
        var pushes = users
            .Select(user => new UserPush(user.Id, 1_700_000_000_000_000, $"user.{user.Id}", user.FirstName, user.LastName, user.Email, user.Enabled, Deleted: false))
            .ToList();

        context.DirectoryUsers.AddRange(ProjectionBatch.Apply(pushes, new Dictionary<string, ProjectedUser>(), DateTimeOffset.UtcNow, snapshotId: null).Added);
        await context.SaveChangesAsync();
    }

    /// <summary>Adds an organization membership as the domain makes it, written directly.</summary>
    public async Task AddMemberAsync(Guid organizationId, string userId, OrganizationRole role = OrganizationRole.Member, string addedBy = Creator)
    {
        await using var context = NewContext();

        context.OrganizationMemberships.Add(OrganizationMembership.Add(organizationId, userId, role, addedBy, Now));
        await context.SaveChangesAsync();
    }

    /// <summary>Adds a team membership as the domain makes it, written directly.</summary>
    public async Task AddTeamMemberAsync(Guid organizationId, Guid teamId, string userId)
    {
        await using var context = NewContext();

        context.TeamMemberships.Add(TeamMembership.Add(teamId, organizationId, userId));
        await context.SaveChangesAsync();
    }

    /// <summary>The first column of every row of <paramref name="sql"/>, as text (<c>NULL</c> is the word <c>NULL</c>).</summary>
    public async Task<List<string>> QueryAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(reader.IsDBNull(0) ? "NULL" : Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }

        return rows;
    }

    /// <summary>
    /// An active organization with its seeded process, one project with its root and two teams of the organization — «Пример проекта»
    /// (<see cref="SeededStructure.FirstTeam"/>) and «Платформа» — <b>both linked to the project</b> (feat-028: a team is the
    /// organization's and a link joins it to the project), written through the domain in one unit of work.
    /// </summary>
    public async Task<SeededStructure> CreateStructureAsync(string? organizationName = null, string projectName = "Пример проекта", string key = "PRM")
    {
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var organizationId = Guid.CreateVersion7();
                var processId = await seed.SeedAsync(organizationId, ct);
                var organization = Organization.Create(
                    organizationId, Name(organizationName ?? UniqueName("Организация")), processId, Now, Creator);
                var project = Project.Create(organizationId, Name(projectName), key, Now, Creator);
                var first = Team.Create(organizationId, Name(projectName));
                var second = Team.Create(organizationId, Name("Платформа"));

                context.Organizations.Add(organization);
                context.Projects.Add(project.Project);
                context.Teams.AddRange(first, second);
                context.Areas.Add(project.Root);
                context.ProjectTeams.AddRange(
                    ProjectTeam.Link(organizationId, project.Project.Id, first.Id, Creator, Now),
                    ProjectTeam.Link(organizationId, project.Project.Id, second.Id, Creator, Now));
                await context.SaveChangesAsync(ct);

                return new SeededStructure(organization, project, first, second);
            },
            CancellationToken.None);
    }

    /// <summary>Adds a team of the organization and links it to each of <paramref name="linkedProjects"/> (feat-028 BR-OL-1, BR-OL-4).</summary>
    public async Task<Team> AddTeamAsync(Guid organizationId, string name, params Guid[] linkedProjects)
    {
        await using var context = NewContext();
        var team = Team.Create(organizationId, Name(name));

        context.Teams.Add(team);
        context.ProjectTeams.AddRange(linkedProjects.Select(projectId => ProjectTeam.Link(organizationId, projectId, team.Id, Creator, Now)));
        await context.SaveChangesAsync();

        return team;
    }

    /// <summary>Links an existing team to a project, recording <see cref="Creator"/> and <see cref="Now"/>.</summary>
    public async Task LinkAsync(Guid organizationId, Guid projectId, Guid teamId)
    {
        await using var context = NewContext();

        context.ProjectTeams.Add(ProjectTeam.Link(organizationId, projectId, teamId, Creator, Now));
        await context.SaveChangesAsync();
    }

    /// <summary>Adds a project with its root (no team, feat-028 BR-OL-2) to an existing organization.</summary>
    public async Task<NewProject> AddProjectAsync(Guid organizationId, string name, string key)
    {
        await using var context = NewContext();
        var project = Project.Create(organizationId, Name(name), key, Now, Creator);

        context.Projects.Add(project.Project);
        context.Areas.Add(project.Root);
        await context.SaveChangesAsync();

        return project;
    }

    /// <summary>
    /// An active organization with its seeded process and one project in the <b>legacy shape</b> the migrations before
    /// mod-017 left (a first team that owns the root and holds it as its default area, a second team with its own default
    /// area), written with SQL. A migration test that rolls the schema back past mod-017 and then needs data written "the old
    /// way" uses it: the current model no longer has those columns, so the model cannot write them. The database must be
    /// rolled back past <c>AddWorkItemOwningTeam</c> and <c>RetireAreaOwnership</c> when this is called.
    /// </summary>
    public async Task<LegacyStructure> CreateLegacyStructureAsync(
        string? organizationName = null, string projectName = "Пример проекта", string key = "PRM")
    {
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        var organizationId = Guid.CreateVersion7();

        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var processId = await seed.SeedAsync(organizationId, ct);

                context.Organizations.Add(
                    Organization.Create(organizationId, Name(organizationName ?? UniqueName("Организация")), processId, Now, Creator));
                await context.SaveChangesAsync(ct);

                return true;
            },
            CancellationToken.None);

        var project = await AddLegacyProjectAsync(organizationId, projectName, key);
        var secondTeam = Guid.CreateVersion7();
        var secondArea = Guid.CreateVersion7();

        await ExecuteAsync(
            """
            INSERT INTO teams (id, project_id, name, normalized_name, status, is_first_team)
            VALUES (@team, @project, 'Платформа', 'ПЛАТФОРМА', 'Active', false);
            INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id)
            VALUES (@area, @project, @root, 'Платформа', 'ПЛАТФОРМА', @team, @team)
            """,
            ("team", secondTeam),
            ("project", project.ProjectId),
            ("area", secondArea),
            ("root", project.RootAreaId));

        return new LegacyStructure(organizationId, project.ProjectId, project.FirstTeamId, project.RootAreaId, secondTeam, secondArea);
    }

    /// <summary>A project with its first team and its root, in the legacy shape (<see cref="CreateLegacyStructureAsync"/>).</summary>
    public async Task<LegacyProject> AddLegacyProjectAsync(Guid organizationId, string name, string key)
    {
        var project = new LegacyProject(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var entityName = Name(name);

        await ExecuteAsync(
            """
            INSERT INTO projects (id, organization_id, name, normalized_name, key, status, created_at, created_by)
            VALUES (@project, @organization, @name, @normalized, @key, 'Active', @now, @creator);
            INSERT INTO teams (id, project_id, name, normalized_name, status, is_first_team)
            VALUES (@team, @project, @name, @normalized, 'Active', true);
            INSERT INTO areas (id, project_id, parent_id, name, normalized_name, owner_team_id, default_of_team_id)
            VALUES (@root, @project, NULL, @name, @normalized, @team, @team)
            """,
            ("project", project.ProjectId),
            ("organization", organizationId),
            ("name", entityName.Value),
            ("normalized", entityName.Normalized),
            ("key", key),
            ("now", Now),
            ("creator", Creator),
            ("team", project.FirstTeamId),
            ("root", project.RootAreaId));

        return project;
    }

    /// <summary>A department (top level), not yet stored.</summary>
    public static Area NewDepartment(Guid organizationId, string name) =>
        Area.CreateDepartment(organizationId, Name(name), null, Creator, Now);

    /// <summary>A sub-area under a project node or a sub-area, not yet stored.</summary>
    public static Area NewSubArea(Area parent, string name) =>
        Area.CreateSubArea(parent, Name(name), null, Creator, Now);

    /// <summary>Adds areas as they are; the caller builds them through the domain.</summary>
    public async Task AddAreasAsync(params Area[] areas)
    {
        await using var context = NewContext();

        context.Areas.AddRange(areas);
        await context.SaveChangesAsync();
    }

    /// <summary>Runs <paramref name="sql"/> on a connection of its own; <c>@id</c> and the other parameters are bound by name.</summary>
    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>What <see cref="OrganizationsDatabaseFixture.CreateStructureAsync"/> wrote.</summary>
public sealed record SeededStructure(Organization Organization, NewProject Project, Team FirstTeam, Team SecondTeam);

/// <summary>What <see cref="OrganizationsDatabaseFixture.CreateLegacyStructureAsync"/> wrote: ids only, the model cannot read that shape.</summary>
public sealed record LegacyStructure(Guid OrganizationId, Guid ProjectId, Guid FirstTeamId, Guid RootAreaId, Guid SecondTeamId, Guid SecondAreaId);

/// <summary>A project in the legacy shape (<see cref="OrganizationsDatabaseFixture.AddLegacyProjectAsync"/>).</summary>
public sealed record LegacyProject(Guid ProjectId, Guid FirstTeamId, Guid RootAreaId);

[CollectionDefinition(Name)]
public sealed class OrganizationsDatabaseCollection : ICollectionFixture<OrganizationsDatabaseFixture>
{
    public const string Name = "Organizations database";
}
