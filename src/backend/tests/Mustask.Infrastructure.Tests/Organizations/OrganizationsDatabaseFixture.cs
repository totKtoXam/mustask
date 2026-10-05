using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Process;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// One disposable PostgreSQL for the organizations tests of this assembly,
/// migrated once with the real migrations. Like the process fixture, it gives
/// every context its connection string explicitly and never touches the
/// process-wide <c>ConnectionStrings__mustask</c>. The database is shared by
/// the collection and never emptied, so every test names its organizations
/// uniquely (<see cref="UniqueName"/>).
/// </summary>
public sealed class OrganizationsDatabaseFixture : IAsyncLifetime
{
    public static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public const string Creator = "8a2f0c1e-0000-4000-8000-00000000c0de";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("mustask")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

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
    public ServiceProvider BuildServices(params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options =>
        {
            options.UseNpgsql(ConnectionString);

            if (interceptors.Length > 0)
            {
                options.AddInterceptors(interceptors);
            }
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProcessSeedStore, ProcessSeedStore>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// An active organization with its seeded process, and one project with its
    /// first team, its root, a second team «Платформа» and that team's area,
    /// written through the domain in one unit of work.
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
                var second = Team.CreateWithArea(project.Project.Id, Name("Платформа"), project.Root);

                context.Organizations.Add(organization);
                context.Projects.Add(project.Project);
                context.Teams.AddRange(project.FirstTeam, second.Team);
                context.Areas.AddRange(project.Root, second.Area);
                await context.SaveChangesAsync(ct);

                return new SeededStructure(organization, project, second);
            },
            CancellationToken.None);
    }

    /// <summary>Adds a project with its first team and root to an existing organization.</summary>
    public async Task<NewProject> AddProjectAsync(Guid organizationId, string name, string key)
    {
        await using var context = NewContext();
        var project = Project.Create(organizationId, Name(name), key, Now, Creator);

        context.Projects.Add(project.Project);
        context.Teams.Add(project.FirstTeam);
        context.Areas.Add(project.Root);
        await context.SaveChangesAsync();

        return project;
    }

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
public sealed record SeededStructure(Organization Organization, NewProject Project, NewTeam SecondTeam);

[CollectionDefinition(Name)]
public sealed class OrganizationsDatabaseCollection : ICollectionFixture<OrganizationsDatabaseFixture>
{
    public const string Name = "Organizations database";
}
