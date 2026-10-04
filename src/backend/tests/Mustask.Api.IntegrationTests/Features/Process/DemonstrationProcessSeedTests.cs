using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mustask.Api.Features.Process;
using Mustask.Application;
using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Process;
using Mustask.Infrastructure;
using Mustask.Infrastructure.Dictionaries.Seeding;
using Mustask.Infrastructure.Process;

namespace Mustask.Api.IntegrationTests.Features.Process;

/// <summary>
/// US-2 (mod-001-process-seed), the Development stand's scenarios (status.md D-4, D-13,
/// BR-M2-1). The shared test host runs in Development, so it already holds the
/// demonstration process; the scenarios the shared database cannot show run on a
/// throw-away database of the same container (<see cref="ScenarioDatabase"/>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DemonstrationProcessSeedTests(MustaskApiFactory factory)
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    private static readonly Guid DemonstrationId = DemonstrationProcessSeed.OrganizationId;

    private static readonly string[] ExpectedTypeKeys = ["EPIC", "FEAT", "US", "BUG", "TASK"];

    // ---- US-2 · "The Development stand shows the demonstration process" -------------------

    [Fact]
    public async Task DevelopmentHost_Started_HasTheDemonstrationProcess()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        var process = await ReadDemonstrationProcessAsync(context);

        Assert.Equal(4, process.BacklogLevels.Count);
        Assert.Equal(ExpectedTypeKeys.Order(StringComparer.Ordinal), process.WorkItemTypes.Select(type => type.Key).Order(StringComparer.Ordinal));
        Assert.Equal(24, process.WorkItemTypes.Sum(type => type.States.Count));
    }

    // The hook is registered once, after the dictionaries seed: hosted services start in
    // registration order, and the dictionaries seed's migrations guard must run first (plan P-1).
    [Fact]
    public void DevelopmentHost_Started_RegistersTheSeedOnceAfterTheDictionariesSeed()
    {
        var hosted = factory.Services.GetServices<IHostedService>().ToList();

        Assert.Single(hosted.OfType<DemonstrationProcessSeed>());

        var dictionaries = hosted.FindIndex(service => service is DictionarySeedHostedService);
        var demonstration = hosted.FindIndex(service => service is DemonstrationProcessSeed);

        Assert.True(dictionaries >= 0, "The dictionaries seed is not registered: the order check proves nothing.");
        Assert.True(dictionaries < demonstration, "The demonstration seed must start after the dictionaries seed.");
    }

    [Fact]
    public async Task Get_DemonstrationOrganization_ReturnsTheFullDefinition()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{DemonstrationId}/process");
        request.WithBearer(KeycloakTestRealm.CreateToken([]));

        using var response = await factory.CreateClient().SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var types = body.RootElement.GetProperty("workItemTypes").EnumerateArray().ToList();

        Assert.Equal(ExpectedTypeKeys, types.Select(type => type.GetProperty("key").GetString()));
        Assert.Equal(24, types.Sum(type => type.GetProperty("states").GetArrayLength()));
        Assert.Equal(4, body.RootElement.GetProperty("backlogLevels").GetArrayLength());
    }

    [Fact]
    public async Task RunAsync_Again_CreatesNothingNew()
    {
        var recorder = new LogRecorder();
        var hook = new DemonstrationProcessSeed(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            recorder.For<DemonstrationProcessSeed>());
        var before = await IdentitiesOfDemonstrationProcessAsync();

        await hook.RunAsync(CancellationToken.None);

        Assert.Equal(before, await IdentitiesOfDemonstrationProcessAsync());

        var ready = Assert.Single(recorder.Of(DemonstrationProcessSeed.ReadyEvent));
        Assert.Equal(LogLevel.Information, ready.Level);
        Assert.Contains(before[0].ToString(), ready.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(recorder.Of(DemonstrationProcessSeed.SkippedEvent));
        Assert.Empty(recorder.Of(DemonstrationProcessSeed.FailedEvent));
    }

    // US-2 · "The Development stand shows the demonstration process" and BR-M2-1: it is
    // Development only, never another environment (status.md D-4).
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void NonDevelopmentHost_DoesNotRegisterTheSeed(string environment)
    {
        // A host of its own replaces Serilog's static logger and disposes it: put it back (see the guard).
        using var loggerGuard = new StaticLoggerGuard();
        using var host = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

        var hosted = host.Services.GetServices<IHostedService>().ToList();

        Assert.Contains(hosted, service => service is DictionarySeedHostedService);
        Assert.DoesNotContain(hosted, service => service is DemonstrationProcessSeed);
    }

    // ---- US-2 · "An unmigrated Development stand starts without the demonstration process" ---

    // The whole host: a database migrated up to, but not including, this task's migration (the
    // stand of tech-002's external database). The dictionaries seed passes, the demonstration
    // seed skips with its warning, and the start completes (status.md D-13, BR-M2-1).
    [Fact]
    public async Task DevelopmentHost_AllButThisTasksMigration_StartsAndLogsSkipped()
    {
        await using var scenario = await ScenarioDatabase.CreateAsync(ScenarioSchema.AllButProcessDefinitions);
        var recorder = new LogRecorder();
        var original = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);

        // MustaskApiFactory hands the connection string over through this process-wide variable
        // (see its summary); switching it is safe because the whole collection runs sequentially,
        // and it is restored in finally.
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, scenario.ConnectionString);

        try
        {
            // A host of its own replaces Serilog's static logger and disposes it: put it back (see the guard).
            using var loggerGuard = new StaticLoggerGuard();
            using var host = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(recorder));
            });
            using var client = host.CreateClient();

            // The start completed: the host answers.
            using var alive = await client.GetAsync("/alive");
            Assert.Equal(HttpStatusCode.OK, alive.StatusCode);

            var skipped = Assert.Single(recorder.Of(DemonstrationProcessSeed.SkippedEvent));
            Assert.Equal(LogLevel.Warning, skipped.Level);
            Assert.Empty(recorder.Of(DemonstrationProcessSeed.ReadyEvent));
            Assert.Empty(recorder.Of(DemonstrationProcessSeed.FailedEvent));
            Assert.False(await scenario.TableExistsAsync("processes"), "Nothing of the process may exist in the unmigrated database.");

            // api-contract.md § 4: until the schema is migrated, a read for the id fails with 500.
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{DemonstrationId}/process");
            request.WithBearer(KeycloakTestRealm.CreateToken([]));
            using var read = await client.SendAsync(request);
            using var problem = JsonDocument.Parse(await read.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.InternalServerError, read.StatusCode);
            Assert.Equal("http.internal_error", problem.RootElement.GetProperty("errorCode").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, original);
        }
    }

    // The hook itself, on a database where not one migration is applied. The whole host cannot
    // start there: the dictionaries seed's migrations guard stops it first, by design
    // (ADR-0002, ADR-0021; plan P-1, status.md D-13). So the hook runs from a host that is
    // built with the real infrastructure and never started.
    [Fact]
    public async Task RunAsync_NoMigrationApplied_LogsSkippedAndCreatesNothing()
    {
        await using var scenario = await ScenarioDatabase.CreateAsync(ScenarioSchema.Empty);
        using var host = BuildUnstartedHost(scenario);
        var recorder = new LogRecorder();
        var hook = new DemonstrationProcessSeed(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            recorder.For<DemonstrationProcessSeed>());
        await using var context = scenario.NewContext();

        await hook.RunAsync(CancellationToken.None);

        var skipped = Assert.Single(recorder.Of(DemonstrationProcessSeed.SkippedEvent));
        Assert.Equal(LogLevel.Warning, skipped.Level);
        Assert.Contains($"{context.Database.GetMigrations().Count()} migration(s)", skipped.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet ef database update", skipped.Message, StringComparison.Ordinal);
        Assert.Empty(recorder.Of(DemonstrationProcessSeed.ReadyEvent));
        Assert.Empty(recorder.Of(DemonstrationProcessSeed.FailedEvent));
        Assert.False(await scenario.TableExistsAsync("processes"));
        Assert.False(await scenario.TableExistsAsync("__EFMigrationsHistory"), "The hook only reads the migration state: it must never apply a migration.");
    }

    // ---- US-2 · "A failed demonstration seed does not stop the stand" -----------------------

    // On a fully migrated scenario database, because the shared one already holds the process.
    // A store decorator lets the real store write every row and then throws, so the rows can only
    // go away because the abandoned unit of work takes them. RunAsync returns instead of throwing,
    // which is what keeps StartingAsync from failing the start; the next run seeds the process.
    [Fact]
    public async Task RunAsync_SeedFailsMidway_LogsFailureLeavesNothingAndNextRunSeeds()
    {
        await using var scenario = await ScenarioDatabase.CreateAsync(ScenarioSchema.Latest);
        var failure = new FailureSwitch();
        using var host = BuildUnstartedHost(scenario, services =>
        {
            services.AddSingleton(failure);
            services.AddScoped<IProcessSeedStore>(provider => new FailAfterWritingStore(
                ActivatorUtilities.CreateInstance<ProcessSeedStore>(provider),
                failure));
        });
        var recorder = new LogRecorder();
        var hook = new DemonstrationProcessSeed(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            recorder.For<DemonstrationProcessSeed>());

        var exception = await Record.ExceptionAsync(() => hook.RunAsync(CancellationToken.None));

        Assert.Null(exception);

        var failed = Assert.Single(recorder.Of(DemonstrationProcessSeed.FailedEvent));
        Assert.Equal(LogLevel.Error, failed.Level);
        Assert.IsType<InvalidOperationException>(failed.Exception);
        Assert.Empty(recorder.Of(DemonstrationProcessSeed.ReadyEvent));
        Assert.Equal((0, 0, 0, 0), await CountRowsAsync(scenario));

        // The next start tries again, and this time nothing fails.
        failure.Armed = false;

        await hook.RunAsync(CancellationToken.None);

        var ready = Assert.Single(recorder.Of(DemonstrationProcessSeed.ReadyEvent));
        Assert.Equal(LogLevel.Information, ready.Level);
        Assert.Equal((1, 4, 5, 24), await CountRowsAsync(scenario));
        Assert.Single(recorder.Of(DemonstrationProcessSeed.FailedEvent));
    }

    // ---- helpers ------------------------------------------------------------------------

    private static async Task<ProcessDefinition> ReadDemonstrationProcessAsync(MustaskDbContext context) =>
        await context.Processes
            .AsNoTracking()
            .Include(process => process.BacklogLevels)
            .Include(process => process.WorkItemTypes).ThenInclude(type => type.States)
            .SingleAsync(process => process.OrganizationId == DemonstrationId);

    /// <summary>The process identity first, then every other identity of the demonstration process, ordered.</summary>
    private async Task<List<Guid>> IdentitiesOfDemonstrationProcessAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        var process = await ReadDemonstrationProcessAsync(context);

        List<Guid> children =
        [
            .. process.BacklogLevels.Select(level => level.Id),
            .. process.WorkItemTypes.Select(type => type.Id),
            .. process.WorkItemTypes.SelectMany(type => type.States).Select(state => state.Id),
        ];
        children.Sort();

        return [process.Id.Value, .. children];
    }

    private static async Task<(int Processes, int Levels, int Types, int States)> CountRowsAsync(ScenarioDatabase scenario)
    {
        await using var context = scenario.NewContext();

        return (
            await context.Processes.CountAsync(),
            await context.BacklogLevels.CountAsync(),
            await context.WorkItemTypes.CountAsync(),
            await context.StateDefinitions.CountAsync());
    }

    /// <summary>
    /// The real infrastructure and application wiring over a scenario database, built and never
    /// started: its hosted services, the dictionaries seed with its migrations guard included, do not run.
    /// </summary>
    private static IHost BuildUnstartedHost(ScenarioDatabase scenario, Action<IServiceCollection>? configure = null)
    {
        // DisableDefaults: the process-wide ConnectionStrings__mustask variable of the shared
        // host must not leak into this one.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
            DisableDefaults = true,
        });
        builder.Configuration["ConnectionStrings:mustask"] = scenario.ConnectionString;
        builder.AddInfrastructure();
        builder.Services.AddApplication();
        configure?.Invoke(builder.Services);

        return builder.Build();
    }

    private sealed class FailureSwitch
    {
        public bool Armed { get; set; } = true;
    }

    /// <summary>The real store writes every row of the process, then the call fails.</summary>
    private sealed class FailAfterWritingStore(IProcessSeedStore inner, FailureSwitch failure) : IProcessSeedStore
    {
        public async Task<ProcessId> InsertIfAbsentAsync(ProcessDefinition candidate, CancellationToken cancellationToken)
        {
            var id = await inner.InsertIfAbsentAsync(candidate, cancellationToken);

            if (failure.Armed)
            {
                throw new InvalidOperationException("Simulated failure after the process rows were written.");
            }

            return id;
        }
    }
}
