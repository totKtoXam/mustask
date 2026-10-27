using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>area_projects</c> (mod-018 BR-AW-6): the link between an area and a project of one organization, the primary key
/// <c>(area_id, project_id)</c>. Both ends are keyed together with the organization copy, so a link across organizations is
/// refused by the database. The link ends with its area (<c>Cascade</c>); a project is never removed.
/// </summary>
internal sealed class AreaProjectConfiguration : IEntityTypeConfiguration<AreaProject>
{
    public void Configure(EntityTypeBuilder<AreaProject> builder)
    {
        builder.HasKey(link => new { link.AreaId, link.ProjectId });
        builder.Property(link => link.OrganizationId).IsRequired();
        builder.Property(link => link.LinkedBy).HasMaxLength(OrganizationLimits.LinkedByMaxLength).IsRequired();
        builder.Property(link => link.LinkedAt).IsRequired();

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(link => new { link.OrganizationId, link.AreaId })
            .HasPrincipalKey(area => new { area.OrganizationId, area.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(link => new { link.OrganizationId, link.ProjectId })
            .HasPrincipalKey(project => new { project.OrganizationId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // A project's areas: the direction the primary key does not serve.
        builder.HasIndex(link => new { link.ProjectId, link.AreaId }, "ix_area_projects_project_id_area_id");

        builder.ToTable("area_projects");
    }
}

/// <summary><c>area_teams</c> (mod-018 BR-AW-6): as <see cref="AreaProjectConfiguration"/>, for a team. It also ends with its team (the handler deletes it, BR-AW-10).</summary>
internal sealed class AreaTeamConfiguration : IEntityTypeConfiguration<AreaTeam>
{
    public void Configure(EntityTypeBuilder<AreaTeam> builder)
    {
        builder.HasKey(link => new { link.AreaId, link.TeamId });
        builder.Property(link => link.OrganizationId).IsRequired();
        builder.Property(link => link.LinkedBy).HasMaxLength(OrganizationLimits.LinkedByMaxLength).IsRequired();
        builder.Property(link => link.LinkedAt).IsRequired();

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(link => new { link.OrganizationId, link.AreaId })
            .HasPrincipalKey(area => new { area.OrganizationId, area.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(link => new { link.OrganizationId, link.TeamId })
            .HasPrincipalKey(team => new { team.OrganizationId, team.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => new { link.TeamId, link.AreaId }, "ix_area_teams_team_id_area_id");

        builder.ToTable("area_teams");
    }
}

/// <summary>
/// <c>area_tree_locks</c>: one row per organization that has written the tree's shape, locked <c>FOR UPDATE</c> by a creation, a
/// move and a deletion before they read the tree they decide on (ADR-0028 rule 5). Written by raw SQL only, like
/// <c>work_item_hierarchy_locks</c>.
/// </summary>
internal sealed class AreaTreeLock
{
    public const string Table = "area_tree_locks";

    private AreaTreeLock()
    {
        // EF materialization.
    }

    public Guid OrganizationId { get; private set; }
}

internal sealed class AreaTreeLockConfiguration : IEntityTypeConfiguration<AreaTreeLock>
{
    public void Configure(EntityTypeBuilder<AreaTreeLock> builder)
    {
        builder.HasKey(row => row.OrganizationId);
        builder.Property(row => row.OrganizationId).ValueGeneratedNever();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(row => row.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(AreaTreeLock.Table);
    }
}
