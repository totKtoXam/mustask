using Mustask.Domain.Dictionaries;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

using Mustask.Domain.UserDirectory;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Conventions;
using Mustask.Infrastructure.Process;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure;

/// <summary>
/// EF Core context for the MUSTASK database. The naming and optimistic-concurrency
/// conventions below are wired here, as model-finalizing conventions, so they
/// apply automatically to every future entity without per-entity configuration
/// (docs/conventions/backend.md, "База данных"). See
/// <see cref="Conventions.SnakeCaseNamingConvention"/>,
/// <see cref="Conventions.OptimisticConcurrencyConvention"/> and
/// <see cref="Conventions.CodeUniqueIndexConvention"/> for why they
/// are registered this way rather than called from
/// <c>OnModelCreating</c>.
/// </summary>
public sealed class MustaskDbContext(DbContextOptions<MustaskDbContext> options) : DbContext(options)
{
    public DbSet<Language> Languages => Set<Language>();

    /// <summary>The process of an organization (<c>processes</c>, doc-007 M2).</summary>
    public DbSet<ProcessDefinition> Processes => Set<ProcessDefinition>();

    public DbSet<BacklogLevel> BacklogLevels => Set<BacklogLevel>();

    public DbSet<WorkItemType> WorkItemTypes => Set<WorkItemType>();

    public DbSet<StateDefinition> StateDefinitions => Set<StateDefinition>();

    public DbSet<ProjectedUser> DirectoryUsers => Set<ProjectedUser>();

    public DbSet<DirectorySnapshot> DirectorySnapshots => Set<DirectorySnapshot>();

    public DbSet<DirectorySnapshotPage> DirectorySnapshotPages => Set<DirectorySnapshotPage>();

    /// <summary>M1's structure (mod-002): organizations, their projects, the projects' teams and area trees.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Team> Teams => Set<Team>();

    /// <summary>feat-028: the links between projects and the organization's teams (<c>project_teams</c>).</summary>
    public DbSet<ProjectTeam> ProjectTeams => Set<ProjectTeam>();

    public DbSet<Area> Areas => Set<Area>();

    /// <summary>Organization access (mod-016): who belongs to which organization with which role, and who is in which team.</summary>
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();

    /// <summary>M3's work items (mod-003); their per-project counter is mapped too, with no DbSet (raw SQL only).</summary>
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // ProcessId is the one typed id that crosses a module seam, so its
        // converter is model-wide: M2's keys and foreign keys get it here, and
        // so will mod-002's Organization.ProcessId, with no configuration of its own.
        configurationBuilder.Properties<ProcessId>().HaveConversion<ProcessIdConverter>();

        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        // CodeUniqueIndexConvention runs BEFORE SnakeCaseNamingConvention:
        // model-finalizing conventions execute in registration order, and the
        // default-named index it adds (e.g. "IX_languages_code") must still
        // exist when SnakeCaseNamingConvention later walks every index to
        // rename it -- registering it after would leave that one index with
        // an un-renamed, non-snake_case name.
        configurationBuilder.Conventions.Add(_ => new CodeUniqueIndexConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MustaskDbContext).Assembly);
    }
}
