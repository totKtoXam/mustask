using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Dictionaries;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Features.Boards;
using Mustask.Application.Features.Organizations;
using Mustask.Application.Features.WorkItems;
using Mustask.Infrastructure.Boards;
using Mustask.Infrastructure.Dictionaries;
using Mustask.Infrastructure.Dictionaries.Seeding;
using Mustask.Infrastructure.Organizations;
using Mustask.Infrastructure.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Infrastructure.UserDirectory;
using Mustask.Infrastructure.WorkItems;
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
        // mod-014: the user projection — the read model other modules'
        // slices consume (ADR-0025 § 9).
        builder.Services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        builder.Services.AddScoped<IProjectedUserStore, ProjectedUserStore>();
        builder.Services.AddScoped<IDirectorySnapshotStore, DirectorySnapshotStore>();

        // Organizations (mod-002): M1's repositories, read models and the reads for other modules.
        builder.Services.AddOrganizationsInfrastructure();

        // Work items (mod-003): M3's repository and read model, and its answer to M1's area-usage port.
        builder.Services.AddWorkItemsInfrastructure();

        // Boards and backlogs (mod-006): M4's backlog read model and the order's repository.
        builder.Services.AddBoardsInfrastructure();

        return builder;
    }

    /// <summary>
    /// The work items module's repository, read model and its implementation of
    /// M1's <see cref="IAreaUsage"/>, all scoped. Public, like
    /// <see cref="AddOrganizationsInfrastructure"/>, so a test host that builds
    /// its own context wires the same set.
    /// </summary>
    public static IServiceCollection AddWorkItemsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IWorkItemRepository, WorkItemRepository>();
        services.AddScoped<IWorkItemReadModel, WorkItemReadModel>();
        services.AddScoped<IAreaUsage, WorkItemAreaUsage>();
        services.AddScoped<IWorkItemTeamOwnership, WorkItemTeamOwnership>();

        return services;
    }

    /// <summary>
    /// The boards module's read model and repository, both scoped. Public, like
    /// <see cref="AddWorkItemsInfrastructure"/>, so a test host that builds its
    /// own context wires the same set.
    /// </summary>
    public static IServiceCollection AddBoardsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBacklogReadModel, BacklogReadModel>();
        services.AddScoped<IBacklogRepository, BacklogRepository>();
        services.AddScoped<IProjectBacklogRepository, ProjectBacklogRepository>();
        services.AddScoped<ITeamBacklogPlaces, TeamBacklogPlaces>();

        return services;
    }

    /// <summary>
    /// The organizations module's repositories, read models and its seam
    /// (<see cref="IOrganizationStructure"/>), all scoped: they share the
    /// request's one database context. Public so a test host that builds its own
    /// context, without Aspire, wires the same set.
    /// </summary>
    public static IServiceCollection AddOrganizationsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IProjectTeamRepository, ProjectTeamRepository>();
        services.AddScoped<IAreaRepository, AreaRepository>();
        services.AddScoped<IOrganizationReadModel, OrganizationReadModel>();
        services.AddScoped<IProjectReadModel, ProjectReadModel>();
        services.AddScoped<ITeamReadModel, TeamReadModel>();
        services.AddScoped<IAreaTreeReadModel, AreaTreeReadModel>();
        services.AddScoped<IOrganizationStructure, OrganizationStructure>();

        // Organization access (mod-016): the memberships' repositories and reads, and the scoped check's one read.
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<ITeamMembershipRepository, TeamMembershipRepository>();
        services.AddScoped<IMemberReadModel, MemberReadModel>();
        services.AddScoped<IOrganizationAccessReadModel, OrganizationAccessReadModel>();

        return services;
    }
}
