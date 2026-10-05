using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>teams</c> (plan-backend § 3). The normalized name is unique among the
/// project's active teams only, so a deleted team frees its name (BR-M1-1,
/// ADR-0026 rule 6). One first team per project, and it is never deleted
/// (BR-M1-9, BR-M1-18). <c>(project_id, id)</c> is an alternate key: the
/// target of the composite keys of <c>areas</c>, which let the database refuse
/// an area owned by another project's team (BR-M1-22, qa.md D-1).
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
        builder.Property(team => team.IsFirstTeam).IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(team => team.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(team => new { team.ProjectId, team.NormalizedName }, OrganizationsConstraints.TeamName)
            .IsUnique()
            .HasFilter("status = 'Active'");
        // The team lists look teams up by project: the two partial indexes above and below do not serve every read.
        builder.HasIndex(team => team.ProjectId, "ix_teams_project_id");
        builder.HasIndex(team => team.ProjectId, OrganizationsConstraints.FirstTeam)
            .IsUnique()
            .HasFilter("is_first_team");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_teams_status", "status IN ('Active', 'Deleted')");
            table.HasCheckConstraint("ck_teams_name_length", OrganizationsConstraints.NameLengthCheck);
            table.HasCheckConstraint("ck_teams_first_team_active", "NOT (is_first_team AND status = 'Deleted')");
        });
    }
}
