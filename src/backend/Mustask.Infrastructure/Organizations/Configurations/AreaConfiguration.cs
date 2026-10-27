using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>areas</c> (mod-018 plan-backend § 3, ADR-0028 rules 4 and 5): one tree per organization. A name is unique among its siblings,
/// the top level included, so the sibling index is <c>NULLS NOT DISTINCT</c> (BR-AW-4). The key to the parent is composite,
/// <c>(organization_id, parent_id)</c> onto the alternate key <c>(organization_id, id)</c>, so the database refuses a parent of
/// another organization (BR-AW-1). A key is <c>Restrict</c>: an area is removed only as a leaf (BR-AW-9). The area owns nothing:
/// projects and teams are linked to it (<see cref="AreaProject"/>, <see cref="AreaTeam"/>).
/// </summary>
internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.HasKey(area => area.Id);
        builder.Property(area => area.Id).ValueGeneratedNever();
        builder.HasAlternateKey(area => new { area.OrganizationId, area.Id });

        builder.Property(area => area.OrganizationId).IsRequired();
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
        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(area => new { area.OrganizationId, area.ParentId })
            .HasPrincipalKey(parent => new { parent.OrganizationId, parent.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(area => new { area.OrganizationId, area.ParentId, area.NormalizedName }, OrganizationsConstraints.AreaName)
            .IsUnique()
            .AreNullsDistinct(false);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_areas_name_length", OrganizationsConstraints.NameLengthCheck);
            table.HasCheckConstraint("ck_areas_description_length", OrganizationsConstraints.DescriptionLengthCheck);
        });
    }
}
