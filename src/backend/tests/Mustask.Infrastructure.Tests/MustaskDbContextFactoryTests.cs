using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests;

// Design-time factory lets
// `dotnet ef migrations add`/`database update` build MustaskDbContext without a
// running Mustask.AppHost. No Docker needed here — building DbContextOptions
// does not open a connection.
[Collection(ProcessEnvironmentCollection.Name)]
public sealed class MustaskDbContextFactoryTests
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    // A hardcoded local-dev connection string (with a literal
    // password) used to be the fallback here, violating AGENTS.md ("secrets
    // only via env/user-secrets, never in code") and, since Aspire assigns a
    // random port and password to the Postgres container it starts, silently
    // pointing at the wrong database instead of failing clearly. There
    // is no local-dev value that could ever be correct, so this now throws.
    [Fact]
    public void CreateDbContext_NoConnectionStringEnvVar_ThrowsWithActionableMessage()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);

        var exception = Assert.Throws<InvalidOperationException>(() => new MustaskDbContextFactory().CreateDbContext([]));

        Assert.Contains(ConnectionStringEnvVar, exception.Message, StringComparison.Ordinal);
        Assert.Contains("AGENTS.md", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateDbContext_ConnectionStringEnvVarSet_UsesIt()
    {
        Environment.SetEnvironmentVariable(
            ConnectionStringEnvVar,
            "Host=custom-host;Database=mustask;Username=u;Password=p");

        try
        {
            using var context = new MustaskDbContextFactory().CreateDbContext([]);

            Assert.Contains("custom-host", context.Database.GetConnectionString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
        }
    }
}
