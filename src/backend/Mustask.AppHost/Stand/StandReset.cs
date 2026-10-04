using System.Diagnostics;
using Mustask.AppHost.Provisioning;
using Npgsql;

namespace Mustask.AppHost.Stand;

/// <summary>
/// The stand reset (mod-014 D-10 (b); ADR-0025 § 7), run by
/// <c>scripts/stand-reset.sh</c> through <c>Mustask.AppHost --stand-reset</c>,
/// so the mode, the volume names and the database come from exactly the code
/// a stand start uses. It wipes the pair — Keycloak's store and the stand's
/// own database — and nothing else:
/// <list type="number">
/// <item>refuses while any container, running or stopped, mounts a volume of
/// the pair ("stop the AppHost first");</item>
/// <item>shows what it will wipe — never a password — and asks, unless
/// <c>--yes</c>;</item>
/// <item>container mode: removes this checkout's PostgreSQL and Keycloak
/// volumes; external mode: on that server's <c>postgres</c> maintenance
/// database, a plain <c>DROP DATABASE</c> of the named database (PostgreSQL
/// itself refuses while anyone is connected) and <c>CREATE DATABASE</c>, then
/// the Keycloak volume keyed to it — never <c>postgres</c>,
/// <c>template0</c>, <c>template1</c> or any other database;</item>
/// <item>deletes the stale E2E stand file and prints the next steps.</item>
/// </list>
/// </summary>
public static class StandReset
{
    private const string ObjectInUse = "55006";

    private const string InvalidCatalogName = "3D000";

    public static async Task<int> RunAsync(StandResetPlan plan, bool confirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var output = Console.Out;

        if ((await DockerAsync(["info", "--format", "{{.ServerVersion}}"], cancellationToken)).ExitCode != 0)
        {
            await Console.Error.WriteLineAsync("Stand reset: Docker is not running — it is needed to remove the stand's volumes.");
            return 1;
        }

        var volumes = plan.Volumes;
        var holders = new List<string>();

        foreach (var volume in volumes)
        {
            var result = await DockerAsync(["ps", "-a", "--filter", "volume=" + volume, "--format", "{{.Names}}"], cancellationToken);
            holders.AddRange(result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (holders.Count > 0)
        {
            await Console.Error.WriteLineAsync(
                "Stand reset refused: these containers use the stand's volumes — "
                + string.Join(", ", holders.Distinct(StringComparer.Ordinal))
                + ". Stop the AppHost first (and remove any leftover container with `docker rm -f <name>`).");
            return 1;
        }

        await output.WriteLineAsync("Stand reset will wipe:");

        foreach (var line in plan.Describe())
        {
            await output.WriteLineAsync("  " + line);
        }

        await output.WriteLineAsync("Nothing else is touched.");

        if (!confirmed)
        {
            if (Console.IsInputRedirected)
            {
                await Console.Error.WriteLineAsync("Stand reset refused: no terminal to confirm on. Run it with --yes to confirm in advance.");
                return 1;
            }

            await output.WriteAsync("Proceed? [y/N] ");
            var answer = Console.ReadLine();

            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(answer?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            {
                await output.WriteLineAsync("Stand reset cancelled; nothing was changed.");
                return 1;
            }
        }

        if (plan is ExternalStandResetPlan external && !await RecreateDatabaseAsync(external, cancellationToken))
        {
            return 1;
        }

        foreach (var volume in volumes)
        {
            if ((await DockerAsync(["volume", "inspect", volume], cancellationToken)).ExitCode != 0)
            {
                await output.WriteLineAsync($"Volume {volume}: not present, nothing to remove.");
                continue;
            }

            var removed = await DockerAsync(["volume", "rm", volume], cancellationToken);

            if (removed.ExitCode != 0)
            {
                await Console.Error.WriteLineAsync($"Stand reset: could not remove volume {volume}: {removed.Error.Trim()}");
                return 1;
            }

            await output.WriteLineAsync($"Volume {volume}: removed.");
        }

        try
        {
            File.Delete(plan.StandFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Console.Error.WriteLineAsync($"Stand reset: could not delete the stale E2E stand file {plan.StandFilePath} ({ex.GetType().Name}).");
        }

        await output.WriteLineAsync();
        await output.WriteLineAsync("Done. Next:");

        foreach (var step in plan.NextSteps())
        {
            await output.WriteLineAsync("  " + step);
        }

        return 0;
    }

    private static async Task<bool> RecreateDatabaseAsync(ExternalStandResetPlan plan, CancellationToken cancellationToken)
    {
        var database = plan.Target.Database;

        if (!StandResetRules.MayReset(database))
        {
            await Console.Error.WriteLineAsync($"Stand reset refused: `{database}` is a PostgreSQL system database; it is never dropped.");
            return false;
        }

        var maintenance = new NpgsqlConnectionStringBuilder(plan.ConnectionString) { Database = "postgres", Pooling = false };

        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var quoted = StandResetRules.QuoteIdentifier(database);

        try
        {
            await using var drop = new NpgsqlCommand("DROP DATABASE " + quoted, connection);
            await drop.ExecuteNonQueryAsync(cancellationToken);
            await Console.Out.WriteLineAsync($"Database {plan.Target}: dropped.");
        }
        catch (PostgresException ex) when (ex.SqlState == ObjectInUse)
        {
            await Console.Error.WriteLineAsync(
                $"Stand reset refused: a stand or another client is connected to {plan.Target}. Stop it and run the reset again.");
            return false;
        }
        catch (PostgresException ex) when (ex.SqlState == InvalidCatalogName)
        {
            await Console.Out.WriteLineAsync($"Database {plan.Target}: not present, nothing to drop.");
        }

        await using var create = new NpgsqlCommand("CREATE DATABASE " + quoted, connection);
        await create.ExecuteNonQueryAsync(cancellationToken);
        await Console.Out.WriteLineAsync($"Database {plan.Target}: created empty.");

        return true;
    }

    private static async Task<(int ExitCode, string Output, string Error)> DockerAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("docker did not start.");
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return (process.ExitCode, await output, await error);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }
}

/// <summary>What one stand's reset wipes — the pair a start of the same AppHost would use.</summary>
public abstract record StandResetPlan(string KeycloakVolume, string StandFilePath)
{
    public abstract IReadOnlyList<string> Volumes { get; }

    public abstract IEnumerable<string> Describe();

    public abstract IEnumerable<string> NextSteps();
}

/// <summary>Container mode: this checkout's own PostgreSQL volume and its Keycloak store.</summary>
public sealed record ContainerStandResetPlan(string PostgresVolume, string KeycloakVolume, string StandFilePath)
    : StandResetPlan(KeycloakVolume, StandFilePath)
{
    public override IReadOnlyList<string> Volumes => [PostgresVolume, KeycloakVolume];

    public override IEnumerable<string> Describe() =>
    [
        "mode: the stand's own PostgreSQL container",
        $"PostgreSQL volume: {PostgresVolume} (the stand's mustask database)",
        $"Keycloak store volume: {KeycloakVolume}",
    ];

    public override IEnumerable<string> NextSteps() =>
    [
        "1. start the stand: dotnet run --project src/backend/Mustask.AppHost",
        "2. apply the migrations to the new database: docs/runbooks/local-migrations.md",
        "3. seed the Keycloak users: bash scripts/keycloak-seed.sh",
        "The first reconciliation of the user projection is the backfill.",
    ];
}

/// <summary>External mode: the one database the connection string names, and the Keycloak store keyed to it.</summary>
public sealed record ExternalStandResetPlan(string ConnectionString, DatabaseTarget Target, string KeycloakVolume, string StandFilePath)
    : StandResetPlan(KeycloakVolume, StandFilePath)
{
    public override IReadOnlyList<string> Volumes => [KeycloakVolume];

    public override IEnumerable<string> Describe() =>
    [
        "mode: external PostgreSQL (ConnectionStrings:mustask)",
        $"database: {Target} — dropped and created empty; no other database on that server is touched",
        $"Keycloak store volume: {KeycloakVolume}",
    ];

    public override IEnumerable<string> NextSteps() =>
    [
        "1. apply the migrations: dotnet ef database update --project src/backend/Mustask.Infrastructure (ConnectionStrings__mustask set)",
        "2. start the stand: dotnet run --project src/backend/Mustask.AppHost",
        "3. seed the Keycloak users: bash scripts/keycloak-seed.sh",
        "The first reconciliation of the user projection is the backfill.",
    ];

    /// <summary>Never prints the connection string: it carries the password.</summary>
    public override string ToString() => $"ExternalStandResetPlan {{ Target = {Target}, KeycloakVolume = {KeycloakVolume} }}";
}
