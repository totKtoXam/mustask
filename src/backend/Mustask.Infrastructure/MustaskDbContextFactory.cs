using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mustask.Infrastructure;

/// <summary>
/// Lets `dotnet ef migrations add` / `dotnet ef database update` build
/// <see cref="MustaskDbContext"/> without running `Mustask.AppHost` — Aspire only
/// injects the "mustask" connection string when the application is actually
/// started. Reads `ConnectionStrings__mustask` (the same env var Aspire sets at
/// runtime). For the Postgres container `Mustask.AppHost` starts, the host port
/// is `Ports:Db` and the password is the `Parameters:mustask-db-password` user
/// secret — fixed per machine, but still a secret, so there is no fallback
/// value in code; docs/runbooks/local-migrations.md has the steps.
/// </summary>
public sealed class MustaskDbContextFactory : IDesignTimeDbContextFactory<MustaskDbContext>
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    public MustaskDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? throw new InvalidOperationException(
                $"Environment variable '{ConnectionStringEnvVar}' is not set. " +
                "There is no local-dev fallback connection string on purpose " +
                "(AGENTS.md: secrets only via env/user-secrets, never in code) " +
                "-- see AGENTS.md for how to obtain and set the actual value " +
                "for the Postgres container Mustask.AppHost started.");

        var optionsBuilder = new DbContextOptionsBuilder<MustaskDbContext>()
            .UseNpgsql(connectionString);

        return new MustaskDbContext(optionsBuilder.Options);
    }
}
