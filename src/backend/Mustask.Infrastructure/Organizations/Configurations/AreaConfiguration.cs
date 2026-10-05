using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>areas</c> (plan-backend § 3). A name is unique among its siblings; the
/// root's null parent means two roots are never compared (BR-M1-12). One root
/// per project, owned explicitly (BR-M1-11). The default area lives on the area
/// row, not on the team: that keeps the foreign keys free of a cycle, and makes
/// "a default area is owned explicitly by its team" a one-line check (BR-M1-14).
/// Every key is <c>Restrict</c>: an area is removed only as a leaf. The keys to
/// the parent and to the two teams are composite, <c>(project_id, …)</c> onto
/// the alternate keys <c>(project_id, id)</c> of <c>areas</c> and <c>teams</c>,
/// so the database refuses a parent or a team of another project (BR-M1-22,
/// qa.md D-1). A null parent or team leaves its key unchecked (MATCH SIMPLE).
/// </summary>
internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.HasKey(area => area.Id);
        builder.Property(area => area.Id).ValueGeneratedNever();
        builder.HasAlternateKey(area => new { area.ProjectId, area.Id });

        builder.Property(area => area.ProjectId).IsRequired();
        builder.Property(area => area.Name).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(area => area.NormalizedName).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(area => area.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(area => new { area.ProjectId, area.ParentId })
            .HasPrincipalKey(parent => new { parent.ProjectId, parent.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(area => new { area.ProjectId, area.OwnerTeamId })
            .HasPrincipalKey(team => new { team.ProjectId, team.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(area => new { area.ProjectId, area.DefaultOfTeamId })
            .HasPrincipalKey(team => new { team.ProjectId, team.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(area => new { area.ParentId, area.NormalizedName }, OrganizationsConstraints.AreaName).IsUnique();
        builder.HasIndex(area => area.ProjectId, OrganizationsConstraints.AreaRoot)
            .IsUnique()
            .HasFilter("parent_id IS NULL");
        // The tree read and every lock by project look areas up by project: the partial root index does not serve them.
        builder.HasIndex(area => area.ProjectId, "ix_areas_project_id");
        builder.HasIndex(area => area.DefaultOfTeamId, OrganizationsConstraints.AreaDefault)
            .IsUnique()
            .HasFilter("default_of_team_id IS NOT NULL");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_areas_name_length", OrganizationsConstraints.NameLengthCheck);
            table.HasCheckConstraint("ck_areas_default_owned", "default_of_team_id IS NULL OR default_of_team_id = owner_team_id");
            table.HasCheckConstraint("ck_areas_root_owned", "parent_id IS NOT NULL OR owner_team_id IS NOT NULL");
        });
    }
}
