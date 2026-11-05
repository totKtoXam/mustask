using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>areas</c> (mod-018 plan-backend § 3, ADR-0028 rules 4 and 5 as ADR-0031 reworks them): one typed tree per organization. A name
/// is unique among its siblings, the top level included, departments and project nodes sharing one name space (OQ-12 A), so the
/// sibling index is <c>NULLS NOT DISTINCT</c> (BR-AW-4). The key to the parent is composite, <c>(organization_id, parent_id)</c> onto
/// the alternate key <c>(organization_id, id)</c>, so the database refuses a parent of another organization (BR-AW-1). A key is
/// <c>Restrict</c>: a node is removed only as a leaf (BR-AW-9). A project's node and its sub-areas carry <c>project_id</c>; a project
/// has one node (the partial unique index); a work item's «Область» is keyed onto <c>(project_id, id)</c> so the database refuses an
/// area of another project (BR-AW-11). The type and its shape are a check. A node owns nothing: teams are linked to a department
/// (<see cref="AreaTeam"/>).
/// </summary>
internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.HasKey(area => area.Id);
        builder.Property(area => area.Id).ValueGeneratedNever();
        builder.HasAlternateKey(area => new { area.OrganizationId, area.Id });
        builder.HasIndex(area => new { area.ProjectId, area.Id }, OrganizationsConstraints.AreaProjectKey).IsUnique();

        builder.Property(area => area.OrganizationId).IsRequired();
        builder.Property(area => area.Type).HasConversion<short>().IsRequired();
        builder.Property(area => area.Name).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(area => area.NormalizedName).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(area => area.Description).HasColumnType("text");
        builder.Property(area => area.InheritsAccess).HasDefaultValue(true).IsRequired();
        builder.Property(area => area.CreatedBy).HasMaxLength(OrganizationLimits.LinkedByMaxLength).IsRequired();
        builder.Property(area => area.CreatedAt).IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(area => area.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(area => area.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(area => new { area.OrganizationId, area.ParentId })
            .HasPrincipalKey(parent => new { parent.OrganizationId, parent.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(area => new { area.OrganizationId, area.ParentId, area.NormalizedName }, OrganizationsConstraints.AreaName)
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(area => area.ProjectId, OrganizationsConstraints.AreaProjectNode)
            .IsUnique()
            .HasFilter("type = 2");
        builder.HasIndex(area => new { area.OrganizationId, area.Type }, "ix_areas_organization_id_type");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_areas_name_length", OrganizationsConstraints.NameLengthCheck);
            table.HasCheckConstraint("ck_areas_description_length", OrganizationsConstraints.DescriptionLengthCheck);
            table.HasCheckConstraint(
                OrganizationsConstraints.AreaTypeShape,
                "(type = 1 AND project_id IS NULL AND parent_id IS NULL) "
                + "OR (type = 2 AND project_id IS NOT NULL) "
                + "OR (type = 3 AND project_id IS NOT NULL AND parent_id IS NOT NULL)");
        });
    }
}
