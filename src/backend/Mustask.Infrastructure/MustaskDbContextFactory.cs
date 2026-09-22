using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mustask.Infrastructure;

/// <summary>
/// Lets `dotnet ef migrations add` / `dotnet ef database update` build
/// <see cref="MustaskDbContext"/> without running `Mustask.AppHost` — Aspire only
/// injects the "mustask" connection string when the application is actually
/// started. Reads `ConnectionStrings__mustask` (the same env var Aspire sets at
/// runtime); see AGENTS.md for how to obtain the actual value of that
/// variable for the Postgres container `Mustask.AppHost` starts (its host port
/// and password are assigned at random, so there is no fixed value to fall
/// back to — see docs/runbooks/local-migrations.md).
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
