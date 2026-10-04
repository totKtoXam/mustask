using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Application.Abstractions.Process;
using Mustask.Infrastructure.Dictionaries;
using Mustask.Infrastructure.Dictionaries.Seeding;
using Mustask.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Mustask.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services (EF Core + Npgsql) using the Aspire
    /// connection string named "mustask" provided by Mustask.AppHost. This also
    /// registers a DB-availability health check by default (Aspire client
    /// integration behavior).
    /// Also registers the concrete implementations of the
    /// Mustask.Application/Abstractions interfaces -- keeping the wiring next to
    /// the DbContext registration it depends on, rather than scattering it
    /// across per-module DI extensions -- plus the dictionaries repositories/
    /// read-models/resolver and their startup seeder, and the process seed
    /// store and read model (mod-001). There are no identity
    /// repositories here: Mustask.Api is a resource server against Keycloak
    /// realm "mustask" (ADR-0018).
    /// </summary>
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // DisableRetry: Aspire's Npgsql EF Core integration enables
        // EnableRetryOnFailure by default, whose retrying execution
        // strategy refuses to run inside a user-initiated
        // Database.BeginTransactionAsync() ("does not support
        // user-initiated transactions"). Repositories following ADR-0011
        // need exactly that (an explicit transaction spanning a pessimistic
        // lock or a SKIP LOCKED select, Mustask.Application logic, and a later
        // commit), and that span cannot be collapsed into one retriable
        // delegate the way CreateExecutionStrategy().ExecuteAsync() would
        // need, since Mustask.Application code runs in between. No code in this
        // project relies on the automatic retry behaviour today (ADR-0012).
        builder.AddNpgsqlDbContext<MustaskDbContext>(
            connectionName: "mustask",
            configureSettings: settings => settings.DisableRetry = true);

        builder.Services.AddSingleton<IClock, SystemClock>();

        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Справочники: репозиторий/read-модель/резолвер языка запроса плюс
        // сид справочников.
        builder.Services.AddScoped<ILanguageRepository, LanguageRepository>();
        builder.Services.AddScoped<ILanguageReadModel, LanguageReadModel>();
        builder.Services.AddScoped<IRequestLanguageResolver, RequestLanguageResolver>();
        builder.Services.AddScoped<DictionarySeeder>();
        builder.Services.AddHostedService<DictionarySeedHostedService>();

        // Process (mod-001): the storage half of the seed seam.
        builder.Services.AddScoped<IProcessSeedStore, ProcessSeedStore>();
        builder.Services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();

        return builder;
    }
}
