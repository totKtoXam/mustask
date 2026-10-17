using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>teams</c> (plan-backend § 3). The normalized name is unique among the
/// project's active teams only, so a deleted team frees its name (BR-M1-1,
/// ADR-0026 rule 6). No team is the first and none is fixed (mod-017 BR-OT-11): the
/// marker and its index and check are gone. <c>(project_id, id)</c> is an alternate key: the
/// target of the composite keys of <c>work_items</c> (the owning team) and of
/// <c>projects</c> (the default team), which let the database refuse a team of
/// another project (BR-M1-22, qa.md D-1, mod-017 BR-OT-2).
/// </summary>
internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(team => team.Id);
        builder.Property(team => team.Id).ValueGeneratedNever();
        builder.HasAlternateKey(team => new { team.ProjectId, team.Id });

        builder.Property(team => team.ProjectId).IsRequired();
        builder.Property(team => team.Name).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(team => team.NormalizedName).HasMaxLength(OrganizationLimits.NameMaxLength).IsRequired();
        builder.Property(team => team.Status)
            .HasConversion<string>()
            .HasMaxLength(OrganizationLimits.StatusMaxLength)
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(team => team.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(team => new { team.ProjectId, team.NormalizedName }, OrganizationsConstraints.TeamName)
            .IsUnique()
            .HasFilter("status = 'Active'");
        // The team lists look teams up by project: the partial index above does not serve every read.
        builder.HasIndex(team => team.ProjectId, "ix_teams_project_id");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_teams_status", "status IN ('Active', 'Deleted')");
            table.HasCheckConstraint("ck_teams_name_length", OrganizationsConstraints.NameLengthCheck);
        });
    }
}
