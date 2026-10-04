using System.Data.Common;
using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Process;
using Mustask.Infrastructure.Process;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Process;

/// <summary>
/// US-2 (mod-001-process-seed), the read model against a real PostgreSQL: the
/// order the server fixes (design R-25, DD-2) and the fixed number of queries
/// (docs/conventions/backend.md, "EF Core").
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(ProcessDatabaseCollection.Name)]
public sealed class ProcessDefinitionReadModelTests(ProcessDatabaseFixture database)
{
    private static async Task<ProcessDefinitionRow?> ReadAsync(IServiceProvider provider, Guid organizationId)
    {
        await using var scope = provider.CreateAsyncScope();
        var readModel = scope.ServiceProvider.GetRequiredService<IProcessDefinitionReadModel>();

        return await readModel.FindByOrganizationAsync(organizationId, CancellationToken.None);
    }

    private ServiceProvider BuildServices(params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();
        services.AddScoped(_ => database.NewContext(interceptors));

        return services.BuildServiceProvider();
    }

    // US-2 · "A seeded process is shown in full": five types in the backlog order of DD-2 (a story before a bug on their shared
    // level), levels by rank descending, states by position, each type's level beside it.
    [Fact]
    public async Task FindByOrganizationAsync_SeededOrganization_ReturnsTheDefinitionInDisplayOrder()
    {
        var organizationId = Guid.NewGuid();
        await using var seeding = database.BuildServices();
        var processId = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(seeding, organizationId);
        await using var provider = BuildServices();

        var row = await ReadAsync(provider, organizationId);

        Assert.NotNull(row);
        Assert.Equal(processId, row.Id);
        Assert.Equal([4, 3, 2, 1], row.BacklogLevels.Select(level => level.Rank));
        Assert.Equal(["EPIC", "FEAT", "US", "BUG", "TASK"], row.WorkItemTypes.Select(type => type.Key));
        Assert.Equal(
            ["Эпики", "Фичи", "Истории", "Истории", "Задачи"],
            row.WorkItemTypes.Select(type => type.BacklogLevelName));
        Assert.All(row.WorkItemTypes, type =>
        {
            Assert.Contains(row.BacklogLevels, level => level.Id == type.BacklogLevelId && level.Name == type.BacklogLevelName);
            Assert.Equal(Enumerable.Range(1, type.States.Count), type.States.Select(state => state.Position));
        });
        Assert.Equal(24, row.WorkItemTypes.Sum(type => type.States.Count));

        var task = row.WorkItemTypes.Single(type => type.Key == "TASK");
        Assert.Equal(["Новая", "Активна", "Закрыта", "Удалена"], task.States.Select(state => state.Name));
        Assert.Equal(
            [StateCategory.Proposed, StateCategory.InProgress, StateCategory.Completed, StateCategory.Removed],
            task.States.Select(state => state.Category));
        Assert.Equal(AppearanceToken.Task, task.Appearance);
        Assert.Equal(BacklogLevelKind.Task, row.BacklogLevels.Single(level => level.Id == task.BacklogLevelId).Kind);
    }

    // US-2 · "A seeded process is shown in full": what is read is what the seed stored, nothing more or less.
    [Fact]
    public async Task FindByOrganizationAsync_SeededOrganization_ReturnsExactlyTheStoredIdentities()
    {
        var organizationId = Guid.NewGuid();
        await using var seeding = database.BuildServices();
        await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(seeding, organizationId);
        var stored = (await database.ReadProcessAsync(organizationId))!;
        await using var provider = BuildServices();

        var row = await ReadAsync(provider, organizationId);

        Assert.Equal(
            ProcessDatabaseFixture.IdentitiesOf(stored).Order(),
            new[] { row!.Id.Value }
                .Concat(row.BacklogLevels.Select(level => level.Id))
                .Concat(row.WorkItemTypes.Select(type => type.Id))
                .Concat(row.WorkItemTypes.SelectMany(type => type.States).Select(state => state.Id))
                .Order());
    }

    // US-2 · "An identifier with no process is not found".
    [Fact]
    public async Task FindByOrganizationAsync_UnknownOrganization_ReturnsNull()
    {
        await using var provider = BuildServices();

        Assert.Null(await ReadAsync(provider, Guid.NewGuid()));
    }

    // docs/conventions/backend.md, "Reads run with a fixed number of queries": three for a process, one for none.
    [Fact]
    public async Task FindByOrganizationAsync_SeededOrganization_IssuesExactlyThreeQueries()
    {
        var organizationId = Guid.NewGuid();
        await using var seeding = database.BuildServices();
        await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(seeding, organizationId);
        var counter = new CommandCountingInterceptor();
        await using var provider = BuildServices(counter);

        await ReadAsync(provider, organizationId);

        Assert.Equal(3, counter.Commands.Count);
        Assert.All(counter.Commands, command => Assert.StartsWith("SELECT", command.TrimStart(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindByOrganizationAsync_UnknownOrganization_IssuesOneQuery()
    {
        var counter = new CommandCountingInterceptor();
        await using var provider = BuildServices(counter);

        await ReadAsync(provider, Guid.NewGuid());

        Assert.Single(counter.Commands);
    }
}

/// <summary>Records the text of every command a context sends.</summary>
public sealed class CommandCountingInterceptor : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
