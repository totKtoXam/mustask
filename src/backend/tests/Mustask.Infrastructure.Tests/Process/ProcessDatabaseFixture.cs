using System.Data.Common;
using System.Text.RegularExpressions;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Process;
using Mustask.Domain.Process;
using Mustask.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Mustask.Infrastructure.Tests.Process;

/// <summary>
/// One disposable PostgreSQL for the process tests of this assembly, migrated
/// once with the real migrations. It does not touch the process-wide
/// <c>ConnectionStrings__mustask</c> variable the other tests of the assembly
/// share: every context here gets its connection string explicitly. The assembly
/// runs serially, so the tests of the collection do not race each other.
/// </summary>
public sealed class ProcessDatabaseFixture : IAsyncLifetime
{
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

    /// <summary>A context of its own, for reading back what a test wrote. It is not the one a seed ran on.</summary>
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
    /// The seam as production wires it, minus Aspire: a scoped context, the unit
    /// of work, the store and the handler behind <see cref="IProcessSeed"/>. A test
    /// resolves <see cref="IUnitOfWork"/> and <see cref="IProcessSeed"/> from one scope.
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

        return services.BuildServiceProvider();
    }

    /// <summary>Seeds <paramref name="organizationId"/> the way <c>mod-002</c> will: the seam inside a unit of work, from one scope.</summary>
    public static async Task<ProcessId> SeedInUnitOfWorkAsync(IServiceProvider provider, Guid organizationId)
    {
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();

        return await unitOfWork.ExecuteInTransactionAsync(ct => seed.SeedAsync(organizationId, ct), CancellationToken.None);
    }

    /// <summary>The process stored for <paramref name="organizationId"/> with all its children, or <c>null</c>.</summary>
    public async Task<ProcessDefinition?> ReadProcessAsync(Guid organizationId)
    {
        await using var context = NewContext();

        return await context.Processes
            .AsNoTracking()
            .Include(process => process.BacklogLevels)
            .Include(process => process.WorkItemTypes).ThenInclude(type => type.States)
            .SingleOrDefaultAsync(process => process.OrganizationId == organizationId);
    }

    /// <summary>Row counts of the four tables. Complete processes only keep them in the ratio 1 : 4 : 5 : 24.</summary>
    public async Task<(int Processes, int Levels, int Types, int States)> CountRowsAsync()
    {
        await using var context = NewContext();

        return (
            await context.Processes.CountAsync(),
            await context.BacklogLevels.CountAsync(),
            await context.WorkItemTypes.CountAsync(),
            await context.StateDefinitions.CountAsync());
    }

    /// <summary>
    /// Waits until some other session of the database waits on a lock, which is
    /// what a second seed for the same organization does behind the first one. It
    /// polls the server's own view of the sessions, so the tests never sleep for
    /// a guessed time.
    /// </summary>
    public async Task WaitForASessionBlockedOnALockAsync(Task? abortWhenCompleted = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            if (abortWhenCompleted is { IsCompleted: true })
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid() AND wait_event_type = 'Lock'";

            if ((long)(await command.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("No session of the database was blocked on a lock within 30 seconds.");
    }

    /// <summary>The content of a process as comparable lines: levels by rank, types by position, states by position. Identities are left out.</summary>
    public static IReadOnlyList<string> Describe(ProcessDefinition process)
    {
        var lines = new List<string>();

        lines.AddRange(process.BacklogLevels
            .OrderByDescending(level => level.Rank)
            .Select(level => $"level {level.Rank} {level.Name} {level.Kind.ToWireName()}"));

        foreach (var type in process.WorkItemTypes.OrderBy(type => type.Position))
        {
            var home = process.BacklogLevels.Single(candidate => candidate.Id == type.BacklogLevelId);
            lines.Add($"type {type.Position} {type.Key} {type.Name} {type.Appearance.ToWireName()} on {home.Name}");

            lines.AddRange(type.States
                .OrderBy(state => state.Position)
                .Select(state => $"state {type.Key} {state.Position} {state.Name} {state.Category}"));
        }

        return lines;
    }

    /// <summary>All identities of a process: the root, its levels, its types and its states.</summary>
    public static IReadOnlyList<Guid> IdentitiesOf(ProcessDefinition process) =>
        [
            process.Id.Value,
            .. process.BacklogLevels.Select(level => level.Id),
            .. process.WorkItemTypes.Select(type => type.Id),
            .. process.WorkItemTypes.SelectMany(type => type.States).Select(state => state.Id),
        ];
}

[CollectionDefinition(Name)]
public sealed class ProcessDatabaseCollection : ICollectionFixture<ProcessDatabaseFixture>
{
    public const string Name = "Process database";
}

