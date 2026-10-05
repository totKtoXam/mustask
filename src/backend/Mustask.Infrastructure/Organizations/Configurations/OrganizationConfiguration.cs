using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>organizations</c> (plan-backend § 3). The normalized name is unique over
/// all rows, whatever their status: a deactivated organization keeps its name
/// for good (BR-M1-1, ADR-0026 rule 6). The process key points into mod-001's
/// <c>processes</c>, one process per organization, and is <c>Restrict</c>:
/// mod-001 never deletes a process, so an organization is never visible
/// without one (BR-M1-3).
/// </summary>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.Id).ValueGeneratedNever();

        builder.Property(organization => organization.Name).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(organization => organization.NormalizedName).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(organization => organization.Status)
            .HasConversion<string>()
            .HasMaxLength(OrganizationLimits.StatusMaxLength)
            .IsRequired();
        builder.Property(organization => organization.ProcessId).IsRequired();
        builder.Property(organization => organization.CreatedAt).IsRequired();
        builder.Property(organization => organization.CreatedBy).HasMaxLength(OrganizationLimits.CreatedByMaxLength).IsRequired();

        builder.HasOne<ProcessDefinition>()
            .WithMany()
            .HasForeignKey(organization => organization.ProcessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(organization => organization.NormalizedName, OrganizationsConstraints.OrganizationName).IsUnique();
        builder.HasIndex(organization => organization.ProcessId, OrganizationsConstraints.OrganizationProcess).IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_organizations_status", "status IN ('Active', 'Deactivated')");
            table.HasCheckConstraint("ck_organizations_name_length", OrganizationsConstraints.NameLengthCheck);
        });
    }
}
