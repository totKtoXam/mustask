using Mustask.Domain.Iterations;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Iterations.Configurations;

/// <summary>
/// <c>sprints</c> (mod-007 plan-backend, "Database"). A sprint belongs to its team for good: the key <c>(organization_id, team_id)</c>
/// onto the team's alternate key keeps it in the team's organization, and <c>(id, team_id)</c> is an alternate key, the target of
/// the work items' composite key that makes an item's sprint always its owning team's. A team is deleted softly, so its sprints stay.
/// </summary>
internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.HasKey(sprint => sprint.Id);
        builder.Property(sprint => sprint.Id).ValueGeneratedNever();
        builder.HasAlternateKey(sprint => new { sprint.Id, sprint.TeamId });

        builder.Property(sprint => sprint.OrganizationId).IsRequired();
        builder.Property(sprint => sprint.TeamId).IsRequired();
        builder.Property(sprint => sprint.Number).IsRequired();
        builder.Property(sprint => sprint.Name).HasMaxLength(SprintLimits.NameMaxLength).IsRequired();
        builder.Property(sprint => sprint.NormalizedName).HasMaxLength(SprintLimits.NameMaxLength).IsRequired();
        builder.Property(sprint => sprint.State).HasConversion<string>().HasMaxLength(SprintLimits.StateMaxLength).IsRequired();
        builder.Property(sprint => sprint.StartDate);
        builder.Property(sprint => sprint.EndDate);
        builder.Property(sprint => sprint.CreatedAt).IsRequired();
        builder.Property(sprint => sprint.CreatedBy).HasMaxLength(128).IsRequired();
        builder.Property(sprint => sprint.ClosedAt);
        builder.Property(sprint => sprint.ClosedBy).HasMaxLength(128);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(sprint => new { sprint.OrganizationId, sprint.TeamId })
            .HasPrincipalKey(team => new { team.OrganizationId, team.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sprint => new { sprint.TeamId, sprint.Number }, IterationsConstraints.TeamNumber).IsUnique();
        builder.HasIndex(sprint => new { sprint.TeamId, sprint.NormalizedName }, IterationsConstraints.TeamName).IsUnique();
        builder.HasIndex(sprint => sprint.TeamId, IterationsConstraints.TeamActive).IsUnique().HasFilter("state = 'Active'");
        builder.HasIndex(sprint => new { sprint.TeamId, sprint.EndDate }, IterationsConstraints.TeamClosedEnd).HasFilter("state = 'Closed'");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_sprints_state", "state IN ('Planned', 'Active', 'Closed')");
            table.HasCheckConstraint("ck_sprints_number_positive", "number >= 1");
            table.HasCheckConstraint("ck_sprints_dates_paired", "(start_date IS NULL) = (end_date IS NULL)");
            table.HasCheckConstraint("ck_sprints_dates_order", "start_date IS NULL OR end_date >= start_date");
            table.HasCheckConstraint("ck_sprints_started_have_dates", "state = 'Planned' OR start_date IS NOT NULL");
            table.HasCheckConstraint("ck_sprints_closed_marks", "(state = 'Closed') = (closed_at IS NOT NULL AND closed_by IS NOT NULL)");
        });
    }
}
