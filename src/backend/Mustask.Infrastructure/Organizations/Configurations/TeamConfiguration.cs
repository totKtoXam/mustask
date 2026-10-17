using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>teams</c> (feat-028 plan-backend § 4.1, ADR-0028 rule 4). A team belongs to its organization: the normalized name is
/// unique among the organization's active teams only, so a deleted team frees its name (BR-OL-3, ADR-0026 rule 6).
/// <c>(organization_id, id)</c> is an alternate key: the target of the composite keys of <c>project_teams</c> and
/// <c>team_memberships</c>, which let the database refuse a link or a membership across organizations (BR-OL-1). No team
/// is the first and none is fixed (mod-017 BR-OT-11). The description is free text, at most 2 000 characters, with the check
/// as the storage backstop of <see cref="EntityDescription"/>.
/// </summary>
internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(team => team.Id);
        builder.Property(team => team.Id).ValueGeneratedNever();
        builder.HasAlternateKey(team => new { team.OrganizationId, team.Id });

        builder.Property(team => team.OrganizationId).IsRequired();
        builder.Property(team => team.Name).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(team => team.NormalizedName).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(team => team.Description).HasColumnType("text");
        builder.Property(team => team.Status)
            .HasConversion<string>()
            .HasMaxLength(OrganizationLimits.StatusMaxLength)
            .IsRequired();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(team => team.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(team => new { team.OrganizationId, team.NormalizedName }, OrganizationsConstraints.TeamName)
            .IsUnique()
            .HasFilter("status = 'Active'");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_teams_status", "status IN ('Active', 'Deleted')");
            table.HasCheckConstraint("ck_teams_name_length", OrganizationsConstraints.NameLengthCheck);
            table.HasCheckConstraint("ck_teams_description_length", OrganizationsConstraints.DescriptionLengthCheck);
        });
    }
}
