using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>project_teams</c> (feat-028 plan-backend § 4.1, BR-OL-4): the link between a project and a team of one organization. The
/// primary key <c>(project_id, team_id)</c> is "one link per pair" and is also the target of the item's owner key and of the
/// project's default-team key, so the database refuses an owner or a default that is not linked. Both ends are keyed together with
/// the organization copy, onto <c>projects (organization_id, id)</c> and <c>teams (organization_id, id)</c>: a link across
/// organizations is refused by the database. Every key is <c>Restrict</c>: a link that an item or a setting references is never
/// removed under it (the unlink reassigns first), and teams and projects are never removed, only their status changes.
/// </summary>
internal sealed class ProjectTeamConfiguration : IEntityTypeConfiguration<ProjectTeam>
{
    public void Configure(EntityTypeBuilder<ProjectTeam> builder)
    {
        builder.HasKey(link => new { link.ProjectId, link.TeamId }).HasName(OrganizationsConstraints.ProjectTeamPrimaryKey);

        builder.Property(link => link.OrganizationId).IsRequired();
        builder.Property(link => link.LinkedBy).HasMaxLength(OrganizationLimits.LinkedByMaxLength).IsRequired();
        builder.Property(link => link.LinkedAt).IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(link => new { link.OrganizationId, link.ProjectId })
            .HasPrincipalKey(project => new { project.OrganizationId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(link => new { link.OrganizationId, link.TeamId })
            .HasPrincipalKey(team => new { team.OrganizationId, team.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // A team's projects: the direction the primary key does not serve.
        builder.HasIndex(link => new { link.TeamId, link.ProjectId }, OrganizationsConstraints.ProjectTeamTeamProject);

        builder.ToTable("project_teams");
    }
}
