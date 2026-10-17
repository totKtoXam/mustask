using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mustask.Infrastructure.Organizations.Configurations;

/// <summary>
/// <c>team_memberships</c> (mod-016 plan-backend § 3.3, P-2). A user is in a team
/// at most once: the primary key <c>(team_id, user_id)</c>. The team is
/// <c>Restrict</c>: teams are soft-deleted, and the deleting handler ends their
/// memberships itself (BR-M1-36). The <c>organization_id</c> copy and the
/// composite key <c>(organization_id, user_id)</c> onto the organization
/// membership make BR-M1-10 (team members are organization members) a fact of the
/// database, and <c>ON DELETE CASCADE</c> ends a removed member's team memberships
/// in the same statement (BR-M1-36); the foreign key's lock on the parent row also
/// closes the add-versus-remove race (NFR Concurrency).
/// </summary>
internal sealed class TeamMembershipConfiguration : IEntityTypeConfiguration<TeamMembership>
{
    public void Configure(EntityTypeBuilder<TeamMembership> builder)
    {
        builder.HasKey(membership => new { membership.TeamId, membership.UserId });

        builder.Property(membership => membership.OrganizationId).IsRequired();
        builder.Property(membership => membership.UserId).HasMaxLength(OrganizationLimits.UserIdMaxLength).IsRequired();

        // feat-028 (ADR-0028 rule 4): the organization copy becomes a real key — (organization_id, team_id) onto the team's
        // alternate key (organization_id, id) — so a membership never names a team of another organization.
        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(membership => new { membership.OrganizationId, membership.TeamId })
            .HasPrincipalKey(team => new { team.OrganizationId, team.Id })
            .HasConstraintName(OrganizationsConstraints.TeamMembershipTeamKey)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrganizationMembership>()
            .WithMany()
            .HasForeignKey(membership => new { membership.OrganizationId, membership.UserId })
            .HasPrincipalKey(membership => new { membership.OrganizationId, membership.UserId })
            .HasConstraintName(OrganizationsConstraints.TeamMembershipOrganizationMembership)
            .OnDelete(DeleteBehavior.Cascade);

        // Also serves "the teams I am in" (the team list's isMember flag).
        builder.HasIndex(
            membership => new { membership.OrganizationId, membership.UserId },
            OrganizationsConstraints.TeamMembershipOrganizationUser);
    }
}
