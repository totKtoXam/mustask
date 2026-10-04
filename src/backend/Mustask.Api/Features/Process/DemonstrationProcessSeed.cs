using Mustask.Application.Abstractions;
using Mustask.Application.Features.Process;
using Mustask.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.Features.Process;

/// <summary>
/// The Development stand's second caller of the process seed, beside the
/// organization creation of <c>mod-002</c> (status.md D-4, BR-M2-1): at start it
/// seeds one process for a fixed demonstration organization identifier, so the
/// read-only process screen has something to show before any organization can be
/// created. <c>Program.cs</c> registers it only when the environment is
/// Development, and it is never in AppHost or in any other environment.
/// <c>mod-002</c> deletes this class, its registration and the matching bullet of
/// <c>src/backend/AGENTS.md</c> when real organization creation lands.
/// </summary>
/// <remarks>
/// <para>
/// It runs in <see cref="StartingAsync"/>, before Kestrel serves, as the dictionaries
/// seed does, so the outcome is settled before any request arrives. Hosted services
/// start in registration order, and this one is registered after
/// <c>AddInfrastructure</c>, so the dictionaries seed and its migrations guard run
/// first: a database with no migration at all already stops the start there, by
/// design (ADR-0002, ADR-0021), and never reaches this class.
/// </para>
/// <para>
/// A failure here never stops the start (BR-M2-1): the seed is a convenience of the
/// stand, not something the application needs to run. The failure is logged as an
/// Error, the seed's own savepoint and the abandoned unit of work leave nothing
/// behind, and the next start tries again.
/// </para>
/// </remarks>
public sealed class DemonstrationProcessSeed(
    IServiceScopeFactory scopeFactory,
    ILogger<DemonstrationProcessSeed> logger) : IHostedLifecycleService
{
    /// <summary>
    /// The demonstration organization identifier (api-contract.md § 4). It is documented
    /// in <c>src/backend/AGENTS.md</c> with the panel address that opens it, and a
    /// contract test keeps the two spellings equal.
    /// </summary>
    public static readonly Guid OrganizationId = new("d0000000-0000-4000-8000-000000000001");

    /// <summary>The database lacks a migration this build ships: nothing was seeded, the start went on.</summary>
    public static readonly EventId SkippedEvent = new(9101, "DemonstrationSeedSkipped");

    /// <summary>The demonstration organization has its complete process (created now or already there).</summary>
    public static readonly EventId ReadyEvent = new(9102, "DemonstrationSeedReady");

    /// <summary>The seed failed: nothing of a process remains, the start went on, the next start tries again.</summary>
    public static readonly EventId FailedEvent = new(9103, "DemonstrationSeedFailed");

    public Task StartingAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);

    /// <summary>
    /// One run of the seed. It returns normally whatever happened, except when the
    /// start itself is cancelled; the outcome is in the log (the three events above).
    /// Public so the tests can run it without starting a host.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
            var pending = await MigrationsState.CountPendingAsync(context, cancellationToken);

            if (pending > 0)
            {
                logger.LogWarning(
                    SkippedEvent,
                    "The demonstration process {OrganizationId} was not seeded: {PendingMigrationCount} migration(s) "
                    + "of this build are not applied. Apply them with "
                    + "'dotnet ef database update --project src/backend/Mustask.Infrastructure' and restart the API.",
                    OrganizationId,
                    pending);

                return;
            }

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();

            // The exception leaves ExecuteInTransactionAsync, so the unit of work is
            // abandoned and rolls back: the second line of defence behind the seed's savepoint.
            var processId = await unitOfWork.ExecuteInTransactionAsync(
                ct => seed.SeedAsync(OrganizationId, ct),
                cancellationToken);

            logger.LogInformation(
                ReadyEvent,
                "The demonstration process {ProcessId} of organization {OrganizationId} is in place.",
                processId.Value,
                OrganizationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                FailedEvent,
                exception,
                "The demonstration process {OrganizationId} could not be seeded. Nothing of it remains, "
                + "the API starts without it, and the next start tries again.",
                OrganizationId);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
