using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>areas</c> (plan-backend § 3). A name is unique among its siblings; the
/// root's null parent means two roots are never compared (BR-M1-12). One root
/// per project (BR-M1-11). An area owns nothing and is the default of no team (mod-017 BR-OT-10): the owner and
/// default-of columns, their keys, index and checks are gone. Every key is <c>Restrict</c>: an area is
/// removed only as a leaf. The key to the parent is composite, <c>(project_id, parent_id)</c> onto the
/// alternate key <c>(project_id, id)</c> of <c>areas</c>, so the database refuses a parent of another
/// project (BR-M1-22, qa.md D-1). A null parent leaves its key unchecked (MATCH SIMPLE).
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

        builder.HasIndex(area => new { area.ParentId, area.NormalizedName }, OrganizationsConstraints.AreaName).IsUnique();
        builder.HasIndex(area => area.ProjectId, OrganizationsConstraints.AreaRoot)
            .IsUnique()
            .HasFilter("parent_id IS NULL");
        // The tree read and every lock by project look areas up by project: the partial root index does not serve them.
        builder.HasIndex(area => area.ProjectId, "ix_areas_project_id");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_areas_name_length", OrganizationsConstraints.NameLengthCheck);
        });
    }
}
